using System.Globalization;
using System.Security.Cryptography;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;
using System.Text.Json;

namespace CarInsurance.Api.Services;

public sealed class ApplicationService(
    IApplicationRepository repository,
    IApplicationDocumentStorage documentStorage,
    IPolicyLifecycleService policyLifecycleService,
    IAuditLogService auditLogService) : IApplicationService
{
    private const long MaxDocumentSizeBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
    private static readonly HashSet<string> PaymentFrequencies = new(StringComparer.Ordinal)
    {
        "annual",
        "semiannual",
        "quarterly"
    };
    private static readonly ApplicationDocumentDefinition[] DocumentDefinitions =
    [
        new(
            "identity_document",
            "身分證正反面",
            [".pdf", ".jpg", ".jpeg", ".png"],
            ["application/pdf", "image/jpeg", "image/png"]),
        new(
            "vehicle_registration",
            "行車執照影本",
            [".pdf", ".jpg", ".jpeg", ".png"],
            ["application/pdf", "image/jpeg", "image/png"])
    ];

    public async Task<IReadOnlyList<InsuranceApplicationDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<InsuranceApplicationDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var application = await repository.GetByIdAsync(id, cancellationToken);
        return application is null ? null : Map(application);
    }

    public async Task<InsuranceApplicationDto?> GetByQuoteIdAsync(int quoteId, CancellationToken cancellationToken)
    {
        var application = await repository.GetByQuoteIdAsync(quoteId, cancellationToken);
        return application is null ? null : Map(application);
    }

    public async Task<InsuranceApplicationDto> CreateAsync(
        ApplicationCreateDto request,
        CancellationToken cancellationToken)
    {
        var quote = await repository.GetQuoteAsync(request.QuoteId, cancellationToken)
            ?? throw new ApplicationNotFoundException($"找不到 QuoteId={request.QuoteId} 的報價。");

        var now = DateTime.UtcNow;
        EnsureQuoteValid(quote, now);

        if (await repository.ExistsForQuoteAsync(request.QuoteId, cancellationToken))
        {
            throw new ApplicationConflictException($"QuoteId={request.QuoteId} 已建立投保申請。");
        }

        var businessDate = GetBusinessDate(now);
        var applicationNumber = await GenerateApplicationNumberAsync(businessDate, cancellationToken);

        var application = new InsuranceApplication
        {
            ApplicationNumber = applicationNumber,
            QuoteId = quote.Id,
            Status = "draft",
            CurrentStep = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(application, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var saved = await repository.GetByIdAsync(application.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立投保申請後無法讀取資料。");
        return Map(saved);
    }

    public Task<InsuranceApplicationDto?> UpdateApplicantAsync(
        int id,
        ApplicantStepDto request,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, 1, cancellationToken, application =>
        {
            var applicantName = RequiredTrimmed(request.ApplicantName, "applicantName");
            var relationship = RequiredTrimmed(request.RelationshipToOwner, "relationshipToOwner");
            var paymentFrequency = NormalizePaymentFrequency(request.PaymentFrequency);

            application.ApplicantName = applicantName;
            application.ApplicantRelationshipToOwner = relationship;
            application.PaymentFrequency = paymentFrequency;
        });

    public Task<InsuranceApplicationDto?> UpdateInsuredAsync(
        int id,
        InsuredStepDto request,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, 2, cancellationToken, application =>
        {
            var name = RequiredTrimmed(request.Name, "name");
            var nationalId = RequiredTrimmed(request.NationalId, "nationalId");
            var relationship = RequiredTrimmed(request.RelationshipToOwner, "relationshipToOwner");
            var phone = RequiredTrimmed(request.Phone, "phone");
            var address = RequiredTrimmed(request.Address, "address");
            ValidateBirthDate(request.BirthDate);
            ValidateInsuredIsVehicleOwner(application, name, nationalId, relationship);

            application.InsuredName = name;
            application.InsuredNationalId = nationalId;
            application.InsuredBirthDate = ToUtcStartOfBusinessDate(request.BirthDate);
            application.InsuredRelationshipToOwner = relationship;
            application.InsuredPhone = phone;
            application.InsuredEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            application.InsuredAddress = address;
        });

    public async Task<InsuranceApplicationDto?> UpdateBeneficiariesAsync(
        int id,
        BeneficiariesStepDto request,
        CancellationToken cancellationToken)
    {
        var application = await GetDraftForUpdateAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var designation = NormalizeBeneficiaryDesignation(request.Designation);
        var beneficiaries = BuildBeneficiaries(application.Id, designation, request.Beneficiaries);

        application.BeneficiaryDesignation = designation;
        repository.ReplaceBeneficiaries(application, beneficiaries);
        TouchStep(application, 3);
        await repository.SaveChangesAsync(cancellationToken);

        return MapReloaded(await repository.GetByIdAsync(id, cancellationToken), id, "儲存受益人後");
    }

    public Task<InsuranceApplicationDto?> UpdateSignatureStatusAsync(
        int id,
        SignatureStatusStepDto request,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, 4, cancellationToken, application =>
        {
            var applicantSigned = HasSignature(application, "applicant");
            var insuredSigned = HasSignature(application, "insured");
            var agentSigned = HasSignature(application, "agent");

            if (request.ApplicantSigned != applicantSigned ||
                request.InsuredSigned != insuredSigned ||
                request.AgentSigned != agentSigned)
            {
                throw new ApplicationValidationException(
                    "signature-status 必須與實際電子簽章紀錄一致，請使用 signatures API 完成或清除簽署。");
            }

            SyncSignatureFlags(application);
        });

    public async Task<InsuranceApplicationDto?> SignAsync(
        int id,
        ApplicationSignatureCreateDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var application = await GetDraftForUpdateAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var signerRole = NormalizeSignerRole(request.SignerRole);
        var signerName = ResolveSignerName(application, signerRole);
        var signatureBytes = DecodeSignaturePng(request.SignatureDataUrl);
        var now = DateTime.UtcNow;
        var sha256 = Convert.ToHexString(SHA256.HashData(signatureBytes)).ToLowerInvariant();
        var signatureDataUrl = $"data:image/png;base64,{Convert.ToBase64String(signatureBytes)}";
        var existing = application.Signatures.SingleOrDefault(x => x.SignerRole == signerRole);

        if (existing is null)
        {
            application.Signatures.Add(new ApplicationSignature
            {
                ApplicationId = application.Id,
                SignerRole = signerRole,
                SignerName = signerName,
                Method = "canvas",
                SignatureDataUrl = signatureDataUrl,
                Sha256 = sha256,
                SignedAtUtc = now,
                IpAddress = NormalizeAuditText(ipAddress, 64),
                UserAgent = NormalizeAuditText(userAgent, 500),
                CreatedAtUtc = now
            });
        }
        else
        {
            existing.SignerName = signerName;
            existing.Method = "canvas";
            existing.SignatureDataUrl = signatureDataUrl;
            existing.Sha256 = sha256;
            existing.SignedAtUtc = now;
            existing.IpAddress = NormalizeAuditText(ipAddress, 64);
            existing.UserAgent = NormalizeAuditText(userAgent, 500);
        }

        SyncSignatureFlags(application);
        TouchStep(application, 4);
        await repository.SaveChangesAsync(cancellationToken);

        return MapReloaded(await repository.GetByIdAsync(id, cancellationToken), id, "儲存電子簽章後");
    }

    public async Task<InsuranceApplicationDto?> ClearSignatureAsync(
        int id,
        string signerRole,
        CancellationToken cancellationToken)
    {
        var application = await GetDraftForUpdateAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var role = NormalizeSignerRole(signerRole);
        var existing = application.Signatures.SingleOrDefault(x => x.SignerRole == role)
            ?? throw new ApplicationSignatureNotFoundException($"找不到 signerRole={role} 的電子簽章。");

        application.Signatures.Remove(existing);
        repository.RemoveSignature(existing);
        SyncSignatureFlags(application);
        TouchStep(application, 4);
        await repository.SaveChangesAsync(cancellationToken);

        return MapReloaded(await repository.GetByIdAsync(id, cancellationToken), id, "清除電子簽章後");
    }

    public async Task<SignatureImageDto?> GetSignatureImageAsync(
        int id,
        string signerRole,
        CancellationToken cancellationToken)
    {
        var application = await repository.GetByIdAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var role = NormalizeSignerRole(signerRole);
        var signature = application.Signatures.SingleOrDefault(x => x.SignerRole == role)
            ?? throw new ApplicationSignatureNotFoundException($"找不到 signerRole={role} 的電子簽章。");
        return new SignatureImageDto(DecodeSignaturePng(signature.SignatureDataUrl), signature.Sha256);
    }

    public async Task<IReadOnlyList<ApplicationDocumentDto>?> GetDocumentsAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var application = await repository.GetByIdAsync(id, cancellationToken);
        return application is null ? null : MapDocuments(application);
    }

    public async Task<IReadOnlyList<ApplicationDocumentDto>?> UploadDocumentAsync(
        int id,
        string documentType,
        string originalFileName,
        string contentType,
        long length,
        Stream stream,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var definition = GetDocumentDefinition(documentType);
        if (length <= 0)
        {
            throw new ApplicationValidationException("上傳檔案不可為空。");
        }

        if (length > MaxDocumentSizeBytes)
        {
            throw new ApplicationValidationException("單一文件大小不可超過 10 MiB。");
        }

        var normalizedFileName = RequiredTrimmed(originalFileName, "fileName");
        var extension = Path.GetExtension(normalizedFileName).ToLowerInvariant();
        if (!definition.Extensions.Contains(extension, StringComparer.Ordinal))
        {
            throw new ApplicationValidationException("文件只接受 PDF、JPG、JPEG 或 PNG。");
        }

        var normalizedContentType = RequiredTrimmed(contentType, "contentType").ToLowerInvariant();
        if (!definition.ContentTypes.Contains(normalizedContentType, StringComparer.Ordinal))
        {
            throw new ApplicationValidationException("文件 Content-Type 只接受 application/pdf、image/jpeg 或 image/png。");
        }

        var matchesExtension = extension switch
        {
            ".pdf" => normalizedContentType == "application/pdf",
            ".jpg" or ".jpeg" => normalizedContentType == "image/jpeg",
            ".png" => normalizedContentType == "image/png",
            _ => false
        };
        if (!matchesExtension)
        {
            throw new ApplicationValidationException("文件副檔名與 Content-Type 不一致。");
        }

        var application = await repository.GetForUpdateAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var existing = application.Documents.SingleOrDefault(x => x.DocumentType == definition.DocumentType);
        if (string.Equals(application.Status, "submitted", StringComparison.OrdinalIgnoreCase) &&
            existing?.Status == "uploaded")
        {
            throw new ApplicationConflictException("投保申請送出後，已完成上傳的文件不可替換；僅缺件或退回補件可重新上傳。");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = existing is null ? null : SerializeDocumentAuditSnapshot(existing);

        var stored = await documentStorage.SaveAsync(application.Id, extension, stream, cancellationToken);
        if (stored.SizeBytes != length)
        {
            await documentStorage.DeleteAsync(application.Id, stored.StoredFileName, cancellationToken);
            throw new ApplicationValidationException("上傳檔案大小與實際內容不一致。");
        }

        var previousStoredFileName = existing?.StoredFileName;
        var now = DateTime.UtcNow;
        var document = existing ?? new ApplicationDocument
        {
            ApplicationId = application.Id,
            DocumentType = definition.DocumentType
        };

        if (existing is null)
        {
            application.Documents.Add(document);
        }

        document.OriginalFileName = Path.GetFileName(normalizedFileName);
        document.StoredFileName = stored.StoredFileName;
        document.ContentType = normalizedContentType;
        document.SizeBytes = stored.SizeBytes;
        document.Sha256 = stored.Sha256;
        document.Status = "uploaded";
        document.ReturnReason = null;
        document.UploadedAtUtc = now;
        document.ReturnedAtUtc = null;
        document.UpdatedAtUtc = now;
        application.UpdatedAtUtc = now;

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await documentStorage.DeleteAsync(application.Id, stored.StoredFileName, CancellationToken.None);
            throw;
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                "application.document.uploaded",
                "ApplicationDocument",
                document.Id.ToString(CultureInfo.InvariantCulture),
                beforeJson,
                SerializeDocumentAuditSnapshot(document),
                actor.SourceIp),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousStoredFileName) &&
            !string.Equals(previousStoredFileName, stored.StoredFileName, StringComparison.Ordinal))
        {
            try
            {
                await documentStorage.DeleteAsync(application.Id, previousStoredFileName, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 舊檔清理失敗不回滾已成功保存的新文件；後續維運可清理孤兒檔案。
            }
        }

        var reloaded = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"儲存文件後無法讀取 ApplicationId={id}。");
        return MapDocuments(reloaded);
    }

    public async Task<ApplicationDocumentContentDto?> GetDocumentContentAsync(
        int id,
        string documentType,
        CancellationToken cancellationToken)
    {
        var definition = GetDocumentDefinition(documentType);
        var application = await repository.GetByIdAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var document = application.Documents.SingleOrDefault(x => x.DocumentType == definition.DocumentType)
            ?? throw new ApplicationDocumentNotFoundException($"找不到 {definition.DisplayName} 文件。");
        var bytes = await documentStorage.ReadAsync(application.Id, document.StoredFileName, cancellationToken)
            ?? throw new ApplicationDocumentNotFoundException($"{definition.DisplayName} 的實體檔案不存在。");

        return new ApplicationDocumentContentDto(
            bytes,
            document.ContentType,
            document.OriginalFileName,
            document.Sha256);
    }

    public async Task<IReadOnlyList<ApplicationDocumentDto>?> ReturnDocumentAsync(
        int id,
        string documentType,
        ApplicationDocumentReturnDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var definition = GetDocumentDefinition(documentType);
        var application = await repository.GetForUpdateAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var document = application.Documents.SingleOrDefault(x => x.DocumentType == definition.DocumentType)
            ?? throw new ApplicationDocumentNotFoundException($"找不到 {definition.DisplayName} 文件。");
        var reason = RequiredTrimmed(request.Reason, "reason");
        if (reason.Length > 500)
        {
            throw new ApplicationValidationException("補件退回原因不可超過 500 字元。");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeDocumentAuditSnapshot(document);
        var now = DateTime.UtcNow;
        document.Status = "returned";
        document.ReturnReason = reason;
        document.ReturnedAtUtc = now;
        document.UpdatedAtUtc = now;
        application.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                "application.document.returned",
                "ApplicationDocument",
                document.Id.ToString(CultureInfo.InvariantCulture),
                beforeJson,
                SerializeDocumentAuditSnapshot(document),
                actor.SourceIp),
            cancellationToken);

        var reloaded = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"退回文件後無法讀取 ApplicationId={id}。");
        return MapDocuments(reloaded);
    }

    public Task<InsuranceApplicationDto?> UpdateDocumentConfirmationAsync(
        int id,
        DocumentConfirmationStepDto request,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, 5, cancellationToken, application =>
        {
            application.TermsAccepted = request.TermsAccepted;
            application.ElectronicPolicyAccepted = request.ElectronicPolicyAccepted;
        });

    public async Task<InsuranceApplicationDto?> SubmitAsync(
        int id,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var application = await GetDraftForUpdateAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        EnsureQuoteValid(application.Quote, now);
        ValidateForSubmit(application);
        var actor = RequireAuditActor(auditActor);
        var before = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = application.Status
        };
        var after = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "submitted"
        };
        if (application.CurrentStep != 5)
        {
            before["currentStep"] = application.CurrentStep;
            after["currentStep"] = 5;
        }

        await policyLifecycleService.PrepareApplicationSubmissionAsync(application.QuoteId, application.Id, cancellationToken);

        application.Status = "submitted";
        application.CurrentStep = 5;
        application.SubmittedAtUtc = now;
        application.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                "application.submitted",
                "InsuranceApplication",
                id.ToString(CultureInfo.InvariantCulture),
                JsonSerializer.Serialize(before, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                JsonSerializer.Serialize(after, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                actor.SourceIp),
            cancellationToken);

        return MapReloaded(await repository.GetByIdAsync(id, cancellationToken), id, "送出投保申請後");
    }

    private static string SerializeDocumentAuditSnapshot(ApplicationDocument document) =>
        JsonSerializer.Serialize(
            new
            {
                document.DocumentType,
                document.Status,
                document.ContentType,
                document.SizeBytes,
                document.Sha256
            },
            AuditJsonOptions);

    private static AuditActorContext RequireAuditActor(AuditActorContext auditActor)
    {
        var actor = auditActor.Actor.Trim();
        var role = auditActor.Role.Trim();
        var sourceIp = auditActor.SourceIp.Trim();
        if (actor.Length == 0 || role.Length == 0 || sourceIp.Length == 0)
        {
            throw new InvalidOperationException("缺少可信的稽核操作人、角色或來源 IP。");
        }

        return new AuditActorContext(actor, role, sourceIp);
    }

    private async Task<InsuranceApplicationDto?> UpdateAsync(
        int id,
        int step,
        CancellationToken cancellationToken,
        Action<InsuranceApplication> apply)
    {
        var application = await GetDraftForUpdateAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        apply(application);
        TouchStep(application, step);
        await repository.SaveChangesAsync(cancellationToken);

        return MapReloaded(await repository.GetByIdAsync(id, cancellationToken), id, $"儲存第 {step} 步後");
    }

    private async Task<InsuranceApplication?> GetDraftForUpdateAsync(int id, CancellationToken cancellationToken)
    {
        var application = await repository.GetForUpdateAsync(id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        if (application.Status == "submitted")
        {
            throw new ApplicationConflictException("投保申請已送出，不可再修改。");
        }

        return application;
    }

    private async Task<string> GenerateApplicationNumberAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var prefix = $"APP-{businessDate:yyyyMM}-";
        var latest = await repository.GetLatestApplicationNumberAsync(prefix, cancellationToken);
        var next = 1;

        if (!string.IsNullOrWhiteSpace(latest))
        {
            var suffix = latest[prefix.Length..];
            if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
            {
                throw new InvalidDataException($"既有投保申請編號格式錯誤：{latest}");
            }

            next = sequence + 1;
        }

        return $"{prefix}{next:0000}";
    }

    private static void EnsureQuoteValid(Quote quote, DateTime now)
    {
        if (now > quote.ValidUntilUtc)
        {
            throw new ApplicationConflictException("報價已逾期，不可建立或送出投保申請。");
        }
    }

    private static string NormalizePaymentFrequency(string value)
    {
        var normalized = RequiredTrimmed(value, "paymentFrequency").ToLowerInvariant();
        if (!PaymentFrequencies.Contains(normalized))
        {
            throw new ApplicationValidationException(
                "paymentFrequency 只接受 annual、semiannual 或 quarterly。");
        }

        return normalized;
    }

    private static string NormalizeBeneficiaryDesignation(string value)
    {
        var normalized = RequiredTrimmed(value, "designation").ToLowerInvariant();
        if (normalized is not ("legal_heirs" or "designated"))
        {
            throw new ApplicationValidationException(
                "designation 只接受 legal_heirs 或 designated。");
        }

        return normalized;
    }

    private static List<ApplicationBeneficiary> BuildBeneficiaries(
        int applicationId,
        string designation,
        IReadOnlyList<BeneficiaryInputDto> inputs)
    {
        if (designation == "legal_heirs")
        {
            if (inputs.Count != 0)
            {
                throw new ApplicationValidationException(
                    "designation=legal_heirs 時 beneficiaries 必須為空。");
            }

            return [];
        }

        if (inputs.Count == 0)
        {
            throw new ApplicationValidationException(
                "designation=designated 時至少需要一位受益人。");
        }

        var beneficiaries = new List<ApplicationBeneficiary>(inputs.Count);
        decimal total = 0m;

        foreach (var input in inputs)
        {
            if (input.Percentage <= 0m || input.Percentage > 100m)
            {
                throw new ApplicationValidationException("每位受益人的 percentage 必須大於 0 且小於等於 100。");
            }

            total += input.Percentage;
            beneficiaries.Add(new ApplicationBeneficiary
            {
                ApplicationId = applicationId,
                Name = RequiredTrimmed(input.Name, "beneficiary.name"),
                NationalId = RequiredTrimmed(input.NationalId, "beneficiary.nationalId"),
                RelationshipToInsured = RequiredTrimmed(
                    input.RelationshipToInsured,
                    "beneficiary.relationshipToInsured"),
                Percentage = input.Percentage
            });
        }

        if (total != 100.00m)
        {
            throw new ApplicationValidationException(
                $"指定受益人比例合計必須為 100.00，目前為 {total:0.##}。");
        }

        return beneficiaries;
    }

    private static void ValidateBirthDate(DateOnly birthDate)
    {
        if (birthDate == default)
        {
            throw new ApplicationValidationException("birthDate 為必填。");
        }

        if (birthDate > GetBusinessDate(DateTime.UtcNow))
        {
            throw new ApplicationValidationException("birthDate 不得晚於今日。");
        }
    }

    private static void ValidateInsuredIsVehicleOwner(
        InsuranceApplication application,
        string name,
        string nationalId,
        string relationship)
    {
        if (relationship != "本人")
        {
            throw new ApplicationValidationException("強制險被保險人與車主關係必須為本人。");
        }

        if (!string.Equals(name, application.Quote.Customer.Name, StringComparison.Ordinal) ||
            !string.Equals(nationalId, application.Quote.Customer.NationalId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationValidationException("強制險被保險人姓名與身分證字號必須與車主一致。");
        }
    }

    private static void ValidateForSubmit(InsuranceApplication application)
    {
        if (string.IsNullOrWhiteSpace(application.ApplicantName) ||
            string.IsNullOrWhiteSpace(application.ApplicantRelationshipToOwner) ||
            string.IsNullOrWhiteSpace(application.PaymentFrequency))
        {
            throw new ApplicationValidationException("要保資料尚未完成。");
        }

        NormalizePaymentFrequency(application.PaymentFrequency);

        if (string.IsNullOrWhiteSpace(application.InsuredName) ||
            string.IsNullOrWhiteSpace(application.InsuredNationalId) ||
            !application.InsuredBirthDate.HasValue ||
            string.IsNullOrWhiteSpace(application.InsuredRelationshipToOwner) ||
            string.IsNullOrWhiteSpace(application.InsuredPhone) ||
            string.IsNullOrWhiteSpace(application.InsuredAddress))
        {
            throw new ApplicationValidationException("被保險人資料尚未完成。");
        }

        var insuredBirthDate = FromUtcToBusinessDate(application.InsuredBirthDate.Value);
        ValidateBirthDate(insuredBirthDate);
        ValidateInsuredIsVehicleOwner(
            application,
            application.InsuredName,
            application.InsuredNationalId,
            application.InsuredRelationshipToOwner);

        if (string.IsNullOrWhiteSpace(application.BeneficiaryDesignation))
        {
            throw new ApplicationValidationException("受益人資料尚未完成。");
        }

        var designation = NormalizeBeneficiaryDesignation(application.BeneficiaryDesignation);
        var existingInputs = application.Beneficiaries
            .Select(x => new BeneficiaryInputDto(
                x.Name,
                x.NationalId,
                x.RelationshipToInsured,
                x.Percentage))
            .ToList();
        BuildBeneficiaries(application.Id, designation, existingInputs);

        if (!HasSignature(application, "applicant") ||
            !HasSignature(application, "insured") ||
            !HasSignature(application, "agent") ||
            !application.ApplicantSigned ||
            !application.InsuredSigned ||
            !application.AgentSigned)
        {
            throw new ApplicationValidationException("要保人、被保險人與招攬人皆須完成有效電子簽章後才能送出。");
        }

        if (!application.TermsAccepted)
        {
            throw new ApplicationValidationException("送出前必須同意保險條款與個資告知事項。");
        }
    }

    private static string NormalizeSignerRole(string value)
    {
        var normalized = RequiredTrimmed(value, "signerRole").ToLowerInvariant();
        if (normalized is not ("applicant" or "insured" or "agent"))
        {
            throw new ApplicationValidationException(
                "signerRole 只接受 applicant、insured 或 agent。");
        }

        return normalized;
    }

    private static string ResolveSignerName(InsuranceApplication application, string signerRole) =>
        signerRole switch
        {
            "applicant" => RequiredTrimmed(application.ApplicantName, "applicantName"),
            "insured" => RequiredTrimmed(application.InsuredName, "insuredName"),
            "agent" => RequiredTrimmed(application.Quote.SalesAgent, "salesAgent"),
            _ => throw new ApplicationValidationException("signerRole 不支援。")
        };

    private static byte[] DecodeSignaturePng(string? dataUrl)
    {
        const string prefix = "data:image/png;base64,";
        var value = RequiredTrimmed(dataUrl, "signatureDataUrl");
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationValidationException("signatureDataUrl 只接受 PNG Data URL。");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(value[prefix.Length..]);
        }
        catch (FormatException)
        {
            throw new ApplicationValidationException("signatureDataUrl 的 Base64 格式無效。");
        }

        if (bytes.Length == 0)
        {
            throw new ApplicationValidationException("電子簽章內容不可為空。");
        }

        if (bytes.Length > 512 * 1024)
        {
            throw new ApplicationValidationException("電子簽章 PNG 不可超過 512 KiB。");
        }

        ReadOnlySpan<byte> pngMagic = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (bytes.Length < pngMagic.Length || !bytes.AsSpan(0, pngMagic.Length).SequenceEqual(pngMagic))
        {
            throw new ApplicationValidationException("電子簽章內容不是有效的 PNG 檔頭。");
        }

        return bytes;
    }

    private static string? NormalizeAuditText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static bool HasSignature(InsuranceApplication application, string signerRole) =>
        application.Signatures.Any(x => x.SignerRole == signerRole);

    private static void SyncSignatureFlags(InsuranceApplication application)
    {
        application.ApplicantSigned = HasSignature(application, "applicant");
        application.InsuredSigned = HasSignature(application, "insured");
        application.AgentSigned = HasSignature(application, "agent");
    }

    private static string RequiredTrimmed(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApplicationValidationException($"{fieldName} 為必填。");
        }

        return value.Trim();
    }

    private static void TouchStep(InsuranceApplication application, int step)
    {
        application.CurrentStep = Math.Max(application.CurrentStep, step);
        application.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static DateOnly GetBusinessDate(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, BusinessTimeZone));

    private static DateTime ToUtcStartOfBusinessDate(DateOnly businessDate)
    {
        var localMidnight = businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, BusinessTimeZone);
    }

    private static DateOnly FromUtcToBusinessDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            BusinessTimeZone));

    private static ApplicationDocumentDefinition GetDocumentDefinition(string documentType)
    {
        var normalized = RequiredTrimmed(documentType, "documentType").ToLowerInvariant();
        return DocumentDefinitions.FirstOrDefault(x => x.DocumentType == normalized)
            ?? throw new ApplicationValidationException(
                "documentType 只接受 identity_document 或 vehicle_registration。");
    }

    private static IReadOnlyList<ApplicationDocumentDto> MapDocuments(InsuranceApplication application) =>
        DocumentDefinitions.Select(definition =>
        {
            var document = application.Documents.SingleOrDefault(x => x.DocumentType == definition.DocumentType);
            return document is null
                ? new ApplicationDocumentDto(
                    null,
                    definition.DocumentType,
                    definition.DisplayName,
                    "missing",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
                : new ApplicationDocumentDto(
                    document.Id,
                    definition.DocumentType,
                    definition.DisplayName,
                    document.Status,
                    document.OriginalFileName,
                    document.ContentType,
                    document.SizeBytes,
                    document.Sha256,
                    document.ReturnReason,
                    DateTime.SpecifyKind(document.UploadedAtUtc, DateTimeKind.Utc),
                    document.ReturnedAtUtc.HasValue
                        ? DateTime.SpecifyKind(document.ReturnedAtUtc.Value, DateTimeKind.Utc)
                        : null);
        }).ToList();

    private static InsuranceApplicationDto MapReloaded(
        InsuranceApplication? application,
        int id,
        string action) =>
        application is null
            ? throw new InvalidOperationException($"{action}無法讀取 ApplicationId={id}。")
            : Map(application);

    private static InsuranceApplicationDto Map(InsuranceApplication application)
    {
        var quote = application.Quote;
        return new InsuranceApplicationDto(
            application.Id,
            application.ApplicationNumber,
            application.QuoteId,
            quote.QuoteNumber,
            application.Status,
            application.CurrentStep,
            quote.CustomerId,
            quote.Customer.Name,
            quote.VehicleId,
            quote.Vehicle.LicensePlate,
            FromUtcToBusinessDate(quote.InsuranceStartDate),
            quote.SalesAgent,
            quote.Channel,
            quote.TotalPremium,
            quote.ValidUntilUtc,
            application.ApplicantName,
            application.ApplicantRelationshipToOwner,
            application.PaymentFrequency,
            application.InsuredName,
            application.InsuredNationalId,
            application.InsuredBirthDate.HasValue
                ? FromUtcToBusinessDate(application.InsuredBirthDate.Value)
                : null,
            application.InsuredRelationshipToOwner,
            application.InsuredPhone,
            application.InsuredEmail,
            application.InsuredAddress,
            application.BeneficiaryDesignation,
            application.Beneficiaries
                .OrderBy(x => x.Id)
                .Select(x => new ApplicationBeneficiaryDto(
                    x.Id,
                    x.Name,
                    x.NationalId,
                    x.RelationshipToInsured,
                    x.Percentage))
                .ToList(),
            application.Signatures
                .OrderBy(x => x.Id)
                .Select(x => new ApplicationSignatureDto(
                    x.Id,
                    x.SignerRole,
                    x.SignerName,
                    x.Method,
                    x.Sha256,
                    DateTime.SpecifyKind(x.SignedAtUtc, DateTimeKind.Utc),
                    x.IpAddress,
                    x.UserAgent))
                .ToList(),
            MapDocuments(application),
            HasSignature(application, "applicant"),
            HasSignature(application, "insured"),
            HasSignature(application, "agent"),
            application.TermsAccepted,
            application.ElectronicPolicyAccepted,
            application.CreatedAtUtc,
            application.UpdatedAtUtc,
            application.SubmittedAtUtc);
    }
}

public sealed class ApplicationValidationException(string message) : Exception(message);

public sealed class ApplicationNotFoundException(string message) : Exception(message);

public sealed class ApplicationConflictException(string message) : Exception(message);

public sealed class ApplicationSignatureNotFoundException(string message) : Exception(message);

public sealed class ApplicationDocumentNotFoundException(string message) : Exception(message);

internal sealed record ApplicationDocumentDefinition(
    string DocumentType,
    string DisplayName,
    string[] Extensions,
    string[] ContentTypes);
