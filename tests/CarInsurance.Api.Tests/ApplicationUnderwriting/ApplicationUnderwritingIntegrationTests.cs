using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.ApplicationUnderwriting;

public sealed class ApplicationUnderwritingIntegrationTests
{
    private const string SignaturePngDataUrl = "data:image/png;base64,iVBORw0KGgo=";

    [Fact]
    public async Task Applications_CreateFromQuote_ThenDuplicate_ReturnsCreatedAndConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var quote = await CreateQuoteAsync(factory, sales, claimCountLast3Years: 1);

        var createResponse = await sales.PostAsJsonAsync("/api/applications", new ApplicationCreateDto(quote.Id));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InsuranceApplicationDto>();
        Assert.NotNull(created);
        Assert.Equal(quote.Id, created.QuoteId);
        Assert.Equal("draft", created.Status);
        Assert.StartsWith("APP-", created.ApplicationNumber);

        var duplicate = await sales.PostAsJsonAsync("/api/applications", new ApplicationCreateDto(quote.Id));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Applications_CreateWithMissingQuote_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/applications", new ApplicationCreateDto(999999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Applications_GetMissing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/applications/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Applications_SubmitWithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/applications/1/submit", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Applications_SubmitAsClaims_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var claims = await TestAuth.CreateAuthenticatedClientAsync(factory, "claims");

        var response = await claims.PostAsync("/api/applications/1/submit", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Applications_SubmitIncompleteAsSales_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var quote = await CreateQuoteAsync(factory, sales, claimCountLast3Years: 1);
        var application = await CreateApplicationAsync(sales, quote.Id);

        var response = await sales.PostAsync($"/api/applications/{application.Id}/submit", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Applications_CompleteFlowAsSales_SubmitsAndWritesAudit()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var application = await CreateSubmittedApplicationAsync(factory, sales, claimCountLast3Years: 1);

        Assert.Equal("submitted", application.Status);
        Assert.Equal(5, application.CurrentStep);
        Assert.NotNull(application.SubmittedAtUtc);
        Assert.True(application.ApplicantSigned);
        Assert.True(application.InsuredSigned);
        Assert.True(application.AgentSigned);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x =>
            x.Action == "application.submitted" && x.ObjectId == application.Id.ToString()));
    }

    [Fact]
    public async Task Applications_UpdateAfterSubmit_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales, claimCountLast3Years: 1);

        var response = await sales.PutAsJsonAsync(
            $"/api/applications/{application.Id}/applicant",
            new ApplicantStepDto("王小明", "本人", "annual"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Underwriting_EvaluateAndGetResults_ForManualReviewApplication_ReturnsManualReview()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales, claimCountLast3Years: 1);

        var evaluateResponse = await sales.PostAsync($"/api/applications/{application.Id}/underwriting/evaluate", null);
        Assert.Equal(HttpStatusCode.OK, evaluateResponse.StatusCode);
        var evaluation = await evaluateResponse.Content.ReadFromJsonAsync<UnderwritingEvaluationDto>();
        Assert.NotNull(evaluation);
        Assert.Equal("manual_review", evaluation.OverallAction);
        Assert.True(evaluation.RequiresManualReview);
        Assert.False(evaluation.HasReject);
        Assert.False(evaluation.BlocksIssue);

        var resultsResponse = await sales.GetAsync($"/api/applications/{application.Id}/underwriting-results");
        Assert.Equal(HttpStatusCode.OK, resultsResponse.StatusCode);
        var results = await resultsResponse.Content.ReadFromJsonAsync<UnderwritingEvaluationDto>();
        Assert.NotNull(results);
        Assert.Equal("manual_review", results.OverallAction);
    }

    [Fact]
    public async Task UnderwritingQueue_SyncCreatesPendingReviewCaseAndListFindsIt()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales, claimCountLast3Years: 1);

        var syncResponse = await sales.PostAsync("/api/underwriting-queue/sync", null);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var sync = await syncResponse.Content.ReadFromJsonAsync<UnderwritingQueueSyncResultDto>();
        Assert.NotNull(sync);
        Assert.True(sync.ProcessedCount >= 1);
        Assert.True(sync.CreatedCount >= 1);

        var listResponse = await sales.GetAsync($"/api/underwriting-queue?keyword={application.ApplicationNumber}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<UnderwritingQueueListDto>();
        Assert.NotNull(list);
        var item = Assert.Single(list.Items, x => x.ApplicationId == application.Id);
        Assert.Equal("pending_review", item.Status);
        Assert.Equal("manual_review", item.OverallAction);
    }

    [Theory]
    [InlineData("/api/underwriting-queue?page=0")]
    [InlineData("/api/underwriting-queue?pageSize=0")]
    [InlineData("/api/underwriting-queue?status=not-a-status")]
    public async Task UnderwritingQueue_InvalidQuery_ReturnsBadRequest(string url)
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingQueue_GetMissing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/underwriting-queue/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingQueue_ApproveWithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/underwriting-queue/1/approve",
            new UnderwritingDecisionRequestDto { Opinion = "approve" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingQueue_ApproveAsSales_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await sales.PostAsJsonAsync(
            "/api/underwriting-queue/1/approve",
            new UnderwritingDecisionRequestDto { Opinion = "approve" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingQueue_ApproveAsUnderwriter_ApprovesWritesAuditAndSecondApproveConflicts()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales, claimCountLast3Years: 1);
        var caseId = await SyncAndGetCaseIdAsync(sales, application.Id);
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");

        var approveResponse = await underwriter.PostAsJsonAsync(
            $"/api/underwriting-queue/{caseId}/approve",
            new UnderwritingDecisionRequestDto { Opinion = "整合測試核准" });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<UnderwritingCaseDetailDto>();
        Assert.NotNull(approved);
        Assert.Equal("approved", approved.Status);
        Assert.Equal("approve", approved.Decision);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            Assert.True(await db.AuditLogs.AnyAsync(x =>
                x.Action == "underwriting.approved" && x.ObjectId == caseId.ToString()));
        }

        var secondApprove = await underwriter.PostAsJsonAsync(
            $"/api/underwriting-queue/{caseId}/approve",
            new UnderwritingDecisionRequestDto { Opinion = "repeat" });
        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    [Fact]
    public async Task UnderwritingQueue_RejectAsSales_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await sales.PostAsJsonAsync(
            "/api/underwriting-queue/1/reject",
            new UnderwritingDecisionRequestDto { Opinion = "reject" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingQueue_RejectAsUnderwriter_RejectsAndWritesAudit()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales, claimCountLast3Years: 1);
        var caseId = await SyncAndGetCaseIdAsync(sales, application.Id);
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");

        var response = await underwriter.PostAsJsonAsync(
            $"/api/underwriting-queue/{caseId}/reject",
            new UnderwritingDecisionRequestDto { Opinion = "整合測試退件" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await response.Content.ReadFromJsonAsync<UnderwritingCaseDetailDto>();
        Assert.NotNull(rejected);
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal("reject", rejected.Decision);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x =>
            x.Action == "underwriting.rejected" && x.ObjectId == caseId.ToString()));
    }

    [Fact]
    public async Task UnderwritingQueue_RequestDocumentsWithBlankOpinion_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales, claimCountLast3Years: 1);
        var caseId = await SyncAndGetCaseIdAsync(sales, application.Id);

        var response = await sales.PostAsJsonAsync(
            $"/api/underwriting-queue/{caseId}/request-documents",
            new UnderwritingDecisionRequestDto { Opinion = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingQueue_RequestDocumentsThenReevaluate_ReturnsToEvaluationState()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales, claimCountLast3Years: 1);
        var caseId = await SyncAndGetCaseIdAsync(sales, application.Id);

        var requestResponse = await sales.PostAsJsonAsync(
            $"/api/underwriting-queue/{caseId}/request-documents",
            new UnderwritingDecisionRequestDto { Opinion = "請補充測試文件" });
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);
        var requested = await requestResponse.Content.ReadFromJsonAsync<UnderwritingCaseDetailDto>();
        Assert.NotNull(requested);
        Assert.Equal("pending_documents", requested.Status);

        var reevaluateResponse = await sales.PostAsync($"/api/underwriting-queue/{caseId}/reevaluate", null);
        Assert.Equal(HttpStatusCode.OK, reevaluateResponse.StatusCode);
        var reevaluated = await reevaluateResponse.Content.ReadFromJsonAsync<UnderwritingCaseDetailDto>();
        Assert.NotNull(reevaluated);
        Assert.Equal("pending_review", reevaluated.Status);
        Assert.Equal("manual_review", reevaluated.OverallAction);
    }

    [Fact]
    public async Task UnderwritingRules_List_ReturnsSeedRules()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/underwriting-rules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rules = await response.Content.ReadFromJsonAsync<List<UnderwritingRuleDto>>();
        Assert.NotNull(rules);
        Assert.Contains(rules, x => x.Code == "UW-014");
        Assert.Contains(rules, x => x.Code == "UW-032");
        Assert.Contains(rules, x => x.Code == "UW-051");
    }

    [Fact]
    public async Task UnderwritingRules_GetMissing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/underwriting-rules/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingRules_CreateWithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/underwriting-rules", CreateRule("TEST-UW-AUTH"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingRules_CreateAsUnderwriter_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");

        var response = await underwriter.PostAsJsonAsync("/api/underwriting-rules", CreateRule("TEST-UW-403"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingRules_AdminCreateUpdateDelete_WorksAndWritesAudit()
    {
        using var factory = new CarInsuranceApiFactory();
        using var admin = await TestAuth.CreateAuthenticatedClientAsync(factory, "admin");

        var createResponse = await admin.PostAsJsonAsync("/api/underwriting-rules", CreateRule("TEST-UW-CRUD"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<UnderwritingRuleDto>();
        Assert.NotNull(created);

        var updateResponse = await admin.PutAsJsonAsync(
            $"/api/underwriting-rules/{created.Id}",
            new UnderwritingRuleUpdateDto
            {
                Code = created.Code,
                Name = "整合測試規則更新",
                ConditionType = "claim_count_last_3_years_gt",
                ConditionJson = "{\"count\":2}",
                Action = "manual_review",
                Message = "更新後訊息",
                IsEnabled = true
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<UnderwritingRuleDto>();
        Assert.NotNull(updated);
        Assert.Equal("整合測試規則更新", updated.Name);

        var deleteResponse = await admin.DeleteAsync($"/api/underwriting-rules/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        Assert.False(await db.UnderwritingRules.AnyAsync(x => x.Id == created.Id));
        Assert.True(await db.AuditLogs.AnyAsync(x =>
            x.Action == "underwriting.rule.created" && x.ObjectId == created.Id.ToString()));
        Assert.True(await db.AuditLogs.AnyAsync(x =>
            x.Action == "underwriting.rule.updated" && x.ObjectId == created.Id.ToString()));
        Assert.True(await db.AuditLogs.AnyAsync(x =>
            x.Action == "underwriting.rule.deleted" && x.ObjectId == created.Id.ToString()));
    }

    [Fact]
    public async Task UnderwritingRules_CreateInvalidAction_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var admin = await TestAuth.CreateAuthenticatedClientAsync(factory, "admin");
        var request = CreateRule("TEST-UW-BAD");
        request.Action = "unknown";

        var response = await admin.PostAsJsonAsync("/api/underwriting-rules", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnderwritingRules_CreateDuplicateCode_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var admin = await TestAuth.CreateAuthenticatedClientAsync(factory, "admin");

        var response = await admin.PostAsJsonAsync("/api/underwriting-rules", CreateRule("UW-014"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static UnderwritingRuleCreateDto CreateRule(string code) => new()
    {
        Code = code,
        Name = "整合測試核保規則",
        ConditionType = "claim_count_last_3_years_gt",
        ConditionJson = "{\"count\":2}",
        Action = "manual_review",
        Message = "整合測試規則命中。",
        IsEnabled = true
    };

    private static async Task<InsuranceApplicationDto> CreateSubmittedApplicationAsync(
        CarInsuranceApiFactory factory,
        HttpClient sales,
        int claimCountLast3Years)
    {
        var quote = await CreateQuoteAsync(factory, sales, claimCountLast3Years);
        var application = await CreateApplicationAsync(sales, quote.Id);

        await EnsureOkAsync(sales.PutAsJsonAsync(
            $"/api/applications/{application.Id}/applicant",
            new ApplicantStepDto("王小明", "本人", "annual")));

        await EnsureOkAsync(sales.PutAsJsonAsync(
            $"/api/applications/{application.Id}/insured",
            new InsuredStepDto(
                "王小明",
                "A123456789",
                new DateOnly(1988, 5, 20),
                "本人",
                "0912345678",
                "ming@example.com",
                "台北市中正區重慶南路一段 1 號")));

        await EnsureOkAsync(sales.PutAsJsonAsync(
            $"/api/applications/{application.Id}/beneficiaries",
            new BeneficiariesStepDto("legal_heirs", Array.Empty<BeneficiaryInputDto>())));

        foreach (var signerRole in new[] { "applicant", "insured", "agent" })
        {
            await EnsureOkAsync(sales.PostAsJsonAsync(
                $"/api/applications/{application.Id}/signatures",
                new ApplicationSignatureCreateDto(signerRole, SignaturePngDataUrl)));
        }

        await UploadVehicleRegistrationAsync(sales, application.Id);

        await EnsureOkAsync(sales.PutAsJsonAsync(
            $"/api/applications/{application.Id}/document-confirmation",
            new DocumentConfirmationStepDto(true, true)));

        var submitResponse = await sales.PostAsync($"/api/applications/{application.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        return await submitResponse.Content.ReadFromJsonAsync<InsuranceApplicationDto>()
            ?? throw new InvalidOperationException("Submit 未回傳 Application DTO。");
    }

    private static async Task<InsuranceApplicationDto> CreateApplicationAsync(HttpClient client, int quoteId)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new ApplicationCreateDto(quoteId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InsuranceApplicationDto>()
            ?? throw new InvalidOperationException("Create application 未回傳 DTO。");
    }

    private static async Task<QuoteDto> CreateQuoteAsync(
        CarInsuranceApiFactory factory,
        HttpClient sales,
        int claimCountLast3Years)
    {
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);
        var response = await sales.PostAsJsonAsync(
            "/api/quotes",
            new QuoteCreateDto(
                1,
                1,
                GetFutureBusinessDate(1),
                "整合測試業務",
                "integration",
                "draft",
                CreateCalculation(rateVersionId, claimCountLast3Years)));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuoteDto>()
            ?? throw new InvalidOperationException("Create quote 未回傳 DTO。");
    }

    private static PremiumCalculationRequestDto CreateCalculation(int rateVersionId, int claimCountLast3Years) => new(
        rateVersionId,
        new DateOnly(1988, 5, 20),
        "male",
        4,
        claimCountLast3Years,
        "自用小客車",
        "TOYOTA-COROLLA",
        new DateOnly(2021, 6, 1),
        "自用",
        null,
        null,
        null,
        null,
        null,
        false,
        null,
        Array.Empty<string>(),
        false,
        false,
        false);

    private static async Task UploadVehicleRegistrationAsync(HttpClient client, int applicationId)
    {
        using var multipart = new MultipartFormDataContent();
        using var file = new ByteArrayContent("integration-test-registration"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipart.Add(file, "file", "vehicle-registration.pdf");

        var response = await client.PostAsync(
            $"/api/applications/{applicationId}/documents/vehicle_registration",
            multipart);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<int> SyncAndGetCaseIdAsync(HttpClient client, int applicationId)
    {
        var sync = await client.PostAsync("/api/underwriting-queue/sync", null);
        sync.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync("/api/underwriting-queue?page=1&pageSize=100");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<UnderwritingQueueListDto>()
            ?? throw new InvalidOperationException("Queue list 未回傳 DTO。");
        return Assert.Single(list.Items, x => x.ApplicationId == applicationId).Id;
    }

    private static async Task EnsureOkAsync(Task<HttpResponseMessage> responseTask)
    {
        using var response = await responseTask;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static DateOnly GetFutureBusinessDate(int days)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)).AddDays(days);
    }

    private static async Task<int> GetSeedRateVersionIdAsync(CarInsuranceApiFactory factory)
    {
        _ = factory.Server;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        return await db.RateVersions
            .Where(x => x.Version == "provisional-v0.1" && x.InsuranceProduct.Code == "CTP-AUTO")
            .Select(x => x.Id)
            .SingleAsync();
    }
}
