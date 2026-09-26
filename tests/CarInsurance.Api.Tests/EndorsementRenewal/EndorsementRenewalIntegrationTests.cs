using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.EndorsementRenewal;

public sealed class EndorsementRenewalIntegrationTests
{
    private const string SignaturePngDataUrl = "data:image/png;base64,iVBORw0KGgo=";

    [Fact]
    public async Task Endorsements_CreateBeforePolicyActive_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateIssuedPolicyAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/endorsements",
            CreateEndorsement(policy));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Endorsements_CreateWithMissingPolicy_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/endorsements",
            new EndorsementCreateDto(
                999999,
                "vehicle_usage_change",
                DateTime.UtcNow.Date,
                "整合測試",
                "整合測試"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Endorsements_CreateWithEffectiveDateOutsidePolicy_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/endorsements",
            new EndorsementCreateDto(
                policy.Id,
                "vehicle_usage_change",
                policy.InsuranceEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
                "日期超出保險期間",
                "T-050G invalid effective date"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Endorsements_DraftCrudAdjustmentAndDuplicate_FollowsContract()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/endorsements",
            CreateEndorsement(policy));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<EndorsementDto>();
        Assert.NotNull(created);
        Assert.Equal("draft", created.Status);
        Assert.Equal(policy.Id, created.PolicyId);
        Assert.StartsWith("END-", created.EndorsementNumber);

        var byIdResponse = await client.GetAsync($"/api/endorsements/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, byIdResponse.StatusCode);

        var byPolicyResponse = await client.GetAsync($"/api/endorsements/by-policy/{policy.Id}");
        Assert.Equal(HttpStatusCode.OK, byPolicyResponse.StatusCode);
        var byPolicy = await byPolicyResponse.Content.ReadFromJsonAsync<List<EndorsementDto>>();
        Assert.NotNull(byPolicy);
        Assert.Contains(byPolicy, x => x.Id == created.Id);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/endorsements/{created.Id}",
            new EndorsementUpdateDto(
                "vehicle_usage_change",
                policy.InsuranceStartDate.AddDays(20).ToDateTime(TimeOnly.MinValue),
                "更新批改原因",
                "更新批改摘要"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<EndorsementDto>();
        Assert.NotNull(updated);
        Assert.Equal("更新批改原因", updated.Reason);
        Assert.Equal("更新批改摘要", updated.ChangeSummary);

        var rateVersionId = await GetSeedRateVersionIdAsync(factory);
        var adjustmentResponse = await client.PostAsJsonAsync(
            $"/api/endorsements/{created.Id}/calculate-adjustment",
            CreateCalculation(rateVersionId) with { ClaimRecordLevel = 5 });
        Assert.Equal(HttpStatusCode.OK, adjustmentResponse.StatusCode);
        var adjusted = await adjustmentResponse.Content.ReadFromJsonAsync<EndorsementDto>();
        Assert.NotNull(adjusted);
        Assert.NotNull(adjusted.OriginalPremium);
        Assert.NotNull(adjusted.RecalculatedPremium);
        Assert.NotNull(adjusted.AdjustmentAmount);
        Assert.NotNull(adjusted.CoverageDays);
        Assert.NotNull(adjusted.RemainingDays);
        Assert.NotNull(adjusted.UnexpiredRatio);
        Assert.NotNull(adjusted.AdjustmentCalculatedAtUtc);

        var duplicate = await client.PostAsJsonAsync(
            "/api/endorsements",
            CreateEndorsement(policy));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Endorsements_SubmitWithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/endorsements/1/submit", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endorsements_SubmitAsUnderwriter_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");

        var response = await underwriter.PostAsync("/api/endorsements/1/submit", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Endorsements_ApproveFlow_TransitionsLifecycleAndWritesAudits()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        using var client = factory.CreateClient();
        var endorsement = await CreateEndorsementAsync(client, policy);
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");
        using var manager = await TestAuth.CreateAuthenticatedClientAsync(factory, "manager");

        var submitResponse = await sales.PostAsync($"/api/endorsements/{endorsement.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<EndorsementDto>();
        Assert.NotNull(submitted);
        Assert.Equal("submitted", submitted.Status);
        Assert.NotNull(submitted.SubmittedAtUtc);

        var submittedLifecycle = await GetLifecycleByApplicationAsync(client, policy.ApplicationId);
        Assert.Equal("endorsement_pending", submittedLifecycle.Status);
        Assert.Equal("endorsement_submitted", submittedLifecycle.LastTransition);

        var secondSubmit = await sales.PostAsync($"/api/endorsements/{endorsement.Id}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, secondSubmit.StatusCode);

        var reviewResponse = await underwriter.PostAsync($"/api/endorsements/{endorsement.Id}/review", null);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var reviewing = await reviewResponse.Content.ReadFromJsonAsync<EndorsementDto>();
        Assert.NotNull(reviewing);
        Assert.Equal("reviewing", reviewing.Status);
        Assert.NotNull(reviewing.ReviewingAtUtc);

        var approveResponse = await manager.PostAsJsonAsync(
            $"/api/endorsements/{endorsement.Id}/approve",
            new EndorsementDecisionDto("T-050G 核准"));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<EndorsementDto>();
        Assert.NotNull(approved);
        Assert.Equal("approved", approved.Status);
        Assert.Equal("T-050G 核准", approved.ReviewOpinion);
        Assert.NotNull(approved.DecidedAtUtc);

        var approvedLifecycle = await GetLifecycleByApplicationAsync(client, policy.ApplicationId);
        Assert.Equal("active", approvedLifecycle.Status);
        Assert.Equal("endorsement_approved", approvedLifecycle.LastTransition);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        var objectId = endorsement.Id.ToString();
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "endorsement.submitted" && x.ObjectId == objectId));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "endorsement.reviewing" && x.ObjectId == objectId));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "endorsement.approved" && x.ObjectId == objectId));
    }

    [Fact]
    public async Task Endorsements_ReviewAsSales_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        using var client = factory.CreateClient();
        var endorsement = await CreateEndorsementAsync(client, policy);
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var submit = await sales.PostAsync($"/api/endorsements/{endorsement.Id}/submit", null);
        submit.EnsureSuccessStatusCode();

        var response = await sales.PostAsync($"/api/endorsements/{endorsement.Id}/review", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Endorsements_ApproveAsUnderwriter_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        using var client = factory.CreateClient();
        var endorsement = await CreateEndorsementAsync(client, policy);
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");
        (await sales.PostAsync($"/api/endorsements/{endorsement.Id}/submit", null)).EnsureSuccessStatusCode();
        (await underwriter.PostAsync($"/api/endorsements/{endorsement.Id}/review", null)).EnsureSuccessStatusCode();

        var response = await underwriter.PostAsJsonAsync(
            $"/api/endorsements/{endorsement.Id}/approve",
            new EndorsementDecisionDto("不應被接受"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Endorsements_RejectFlow_ReturnsPolicyToActive()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        using var client = factory.CreateClient();
        var endorsement = await CreateEndorsementAsync(client, policy);
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");
        using var manager = await TestAuth.CreateAuthenticatedClientAsync(factory, "manager");
        (await sales.PostAsync($"/api/endorsements/{endorsement.Id}/submit", null)).EnsureSuccessStatusCode();
        (await underwriter.PostAsync($"/api/endorsements/{endorsement.Id}/review", null)).EnsureSuccessStatusCode();

        var response = await manager.PostAsJsonAsync(
            $"/api/endorsements/{endorsement.Id}/reject",
            new EndorsementDecisionDto("資料需重新確認"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<EndorsementDto>();
        Assert.NotNull(returned);
        Assert.Equal("returned", returned.Status);
        Assert.Equal("資料需重新確認", returned.ReviewOpinion);

        var lifecycle = await GetLifecycleByApplicationAsync(client, policy.ApplicationId);
        Assert.Equal("active", lifecycle.Status);
        Assert.Equal("endorsement_returned", lifecycle.LastTransition);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x =>
            x.Action == "endorsement.returned" && x.ObjectId == endorsement.Id.ToString()));
    }

    [Fact]
    public async Task Endorsements_DeleteDraftButNotSubmitted_FollowsStatusGuard()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        using var client = factory.CreateClient();

        var draft = await CreateEndorsementAsync(client, policy);
        var deleteDraft = await client.DeleteAsync($"/api/endorsements/{draft.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteDraft.StatusCode);

        var submittedCandidate = await CreateEndorsementAsync(client, policy);
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        (await sales.PostAsync($"/api/endorsements/{submittedCandidate.Id}/submit", null)).EnsureSuccessStatusCode();

        var deleteSubmitted = await client.DeleteAsync($"/api/endorsements/{submittedCandidate.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteSubmitted.StatusCode);
    }

    [Fact]
    public async Task Renewals_CreateQuoteWithMissingPolicy_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/renewals/999999/quote", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Renewals_CreateQuoteOutsideRenewalWindow_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/renewals/{policy.Id}/quote", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Renewals_GetEligible_TransitionsActivePolicyToRenewalPending()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        var sourceEndDate = await MovePolicyIntoRenewalWindowAsync(factory, policy.Id, 30);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/renewals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<RenewalListItemDto>>();
        Assert.NotNull(items);
        var item = Assert.Single(items, x => x.PolicyId == policy.Id);
        Assert.Equal(sourceEndDate, item.InsuranceEndDate);
        Assert.Equal(30, item.DaysUntilExpiry);
        Assert.Equal("renewal_pending", item.LifecycleStatus);
        Assert.Null(item.ExistingRenewalQuoteId);

        var lifecycle = await GetLifecycleByApplicationAsync(client, policy.ApplicationId);
        Assert.Equal("renewal_pending", lifecycle.Status);
        Assert.Equal("renewal_window_entered", lifecycle.LastTransition);
    }

    [Fact]
    public async Task Renewals_CreateQuote_CreatesLinkedRenewalAndRejectsDuplicate()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateActivePolicyAsync(factory);
        var sourceEndDate = await MovePolicyIntoRenewalWindowAsync(factory, policy.Id, 30);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/renewals/{policy.Id}/quote", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<QuoteDto>();
        Assert.NotNull(quote);
        Assert.Equal(policy.Id, quote.RenewalSourcePolicyId);
        Assert.Equal("draft", quote.Status);
        Assert.True(quote.Calculation.IsRenewal);
        Assert.Equal(sourceEndDate.AddDays(1), quote.InsuranceStartDate);

        var lifecycle = await GetLifecycleByApplicationAsync(client, policy.ApplicationId);
        Assert.Equal("quotation", lifecycle.Status);
        Assert.Equal("renewal_quote_created", lifecycle.LastTransition);

        var duplicate = await client.PostAsync($"/api/renewals/{policy.Id}/quote", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var eligibleResponse = await client.GetAsync("/api/renewals");
        Assert.Equal(HttpStatusCode.OK, eligibleResponse.StatusCode);
        var eligible = await eligibleResponse.Content.ReadFromJsonAsync<List<RenewalListItemDto>>();
        Assert.NotNull(eligible);
        var item = Assert.Single(eligible, x => x.PolicyId == policy.Id);
        Assert.Equal("quotation", item.LifecycleStatus);
        Assert.Equal(quote.Id, item.ExistingRenewalQuoteId);
    }

    private static EndorsementCreateDto CreateEndorsement(PolicyDto policy) =>
        new(
            policy.Id,
            "vehicle_usage_change",
            policy.InsuranceStartDate.AddDays(10).ToDateTime(TimeOnly.MinValue),
            "T-050G 批改原因",
            "T-050G 批改摘要");

    private static async Task<EndorsementDto> CreateEndorsementAsync(HttpClient client, PolicyDto policy)
    {
        var response = await client.PostAsJsonAsync("/api/endorsements", CreateEndorsement(policy));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EndorsementDto>()
            ?? throw new InvalidOperationException("Create endorsement 未回傳 DTO。");
    }

    private static async Task<PolicyDto> CreateActivePolicyAsync(CarInsuranceApiFactory factory)
    {
        var policy = await CreateIssuedPolicyAsync(factory);
        using var client = factory.CreateClient();
        var lifecycle = await GetLifecycleByApplicationAsync(client, policy.ApplicationId);
        var response = await client.PostAsJsonAsync(
            $"/api/policy-lifecycles/{lifecycle.Id}/transition",
            new PolicyLifecycleTransitionDto(
                "active",
                "policy_effective",
                "T-050G integration test activates issued policy"));
        response.EnsureSuccessStatusCode();
        var active = await response.Content.ReadFromJsonAsync<PolicyLifecycleDto>()
            ?? throw new InvalidOperationException("Activate lifecycle 未回傳 DTO。");
        Assert.Equal("active", active.Status);
        return policy;
    }

    private static async Task<DateOnly> MovePolicyIntoRenewalWindowAsync(
        CarInsuranceApiFactory factory,
        int policyId,
        int daysUntilExpiry)
    {
        var businessDate = GetFutureBusinessDate(0);
        var endDate = businessDate.AddDays(daysUntilExpiry);
        var startDate = endDate.AddYears(-1).AddDays(1);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        var policy = await db.Policies.SingleAsync(x => x.Id == policyId);
        policy.InsuranceStartDate = ToUtcStartOfBusinessDate(startDate);
        policy.InsuranceEndDate = ToUtcStartOfBusinessDate(endDate);
        await db.SaveChangesAsync();
        return endDate;
    }

    private static DateTime ToUtcStartOfBusinessDate(DateOnly date)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        return TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            tz);
    }

    private static PaymentCreateDto CreatePayment(int applicationId) =>
        new(applicationId, "annual", "credit_card");

    private static async Task<PaymentDto> CreatePaymentAsync(HttpClient client, int applicationId)
    {
        var response = await client.PostAsJsonAsync("/api/payments", CreatePayment(applicationId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentDto>()
            ?? throw new InvalidOperationException("Create payment 未回傳 DTO。");
    }

    private static async Task<PaymentDto> ConfirmPaymentAsync(HttpClient underwriter, int paymentId)
    {
        var response = await underwriter.PostAsJsonAsync(
            $"/api/payments/{paymentId}/confirm",
            new PaymentConfirmDto("T050G-CONFIRM", "T-050G 整合測試"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentDto>()
            ?? throw new InvalidOperationException("Confirm payment 未回傳 DTO。");
    }

    private static async Task<PolicyDto> CreateIssuedPolicyAsync(CarInsuranceApiFactory factory)
    {
        var state = await CreatePendingPaymentApplicationAsync(factory);
        using var client = factory.CreateClient();
        var payment = await CreatePaymentAsync(client, state.Application.Id);
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");
        _ = await ConfirmPaymentAsync(underwriter, payment.Id);

        var issue = await underwriter.PostAsync($"/api/policies/issue/{state.Application.Id}", null);
        issue.EnsureSuccessStatusCode();
        return await issue.Content.ReadFromJsonAsync<PolicyDto>()
            ?? throw new InvalidOperationException("Issue policy 未回傳 DTO。");
    }

    private static async Task<(InsuranceApplicationDto Application, int UnderwritingCaseId)> CreatePendingPaymentApplicationAsync(
        CarInsuranceApiFactory factory)
    {
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales);
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");
        var caseId = await ApproveUnderwritingAsync(sales, underwriter, application.Id);
        return (application, caseId);
    }

    private static async Task<int> ApproveUnderwritingAsync(HttpClient syncClient, HttpClient underwriter, int applicationId)
    {
        var sync = await syncClient.PostAsync("/api/underwriting-queue/sync", null);
        sync.EnsureSuccessStatusCode();

        var listResponse = await syncClient.GetAsync("/api/underwriting-queue?page=1&pageSize=100");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<UnderwritingQueueListDto>()
            ?? throw new InvalidOperationException("Underwriting queue 未回傳 DTO。");
        var caseId = Assert.Single(list.Items, x => x.ApplicationId == applicationId).Id;

        var approve = await underwriter.PostAsJsonAsync(
            $"/api/underwriting-queue/{caseId}/approve",
            new UnderwritingDecisionRequestDto { Opinion = "T-050G 整合測試核准" });
        approve.EnsureSuccessStatusCode();
        return caseId;
    }

    private static async Task<PolicyLifecycleDto> GetLifecycleByApplicationAsync(HttpClient client, int applicationId)
    {
        var response = await client.GetAsync($"/api/policy-lifecycles/by-application/{applicationId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PolicyLifecycleDto>()
            ?? throw new InvalidOperationException("Lifecycle 未回傳 DTO。");
    }

    private static async Task<InsuranceApplicationDto> CreateSubmittedApplicationAsync(
        CarInsuranceApiFactory factory,
        HttpClient sales)
    {
        var quote = await CreateQuoteAsync(factory, sales);
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

        var submit = await sales.PostAsync($"/api/applications/{application.Id}/submit", null);
        submit.EnsureSuccessStatusCode();
        return await submit.Content.ReadFromJsonAsync<InsuranceApplicationDto>()
            ?? throw new InvalidOperationException("Submit application 未回傳 DTO。");
    }

    private static async Task<InsuranceApplicationDto> CreateApplicationAsync(HttpClient client, int quoteId)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new ApplicationCreateDto(quoteId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InsuranceApplicationDto>()
            ?? throw new InvalidOperationException("Create application 未回傳 DTO。");
    }

    private static async Task<QuoteDto> CreateQuoteAsync(CarInsuranceApiFactory factory, HttpClient sales)
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
                CreateCalculation(rateVersionId)));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuoteDto>()
            ?? throw new InvalidOperationException("Create quote 未回傳 DTO。");
    }

    private static PremiumCalculationRequestDto CreateCalculation(int rateVersionId) => new(
        rateVersionId,
        new DateOnly(1988, 5, 20),
        "male",
        4,
        1,
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
