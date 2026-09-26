using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.Claims;

public sealed class ClaimIntegrationTests
{
    private const string SignaturePngDataUrl = "data:image/png;base64,iVBORw0KGgo=";

    [Fact]
    public async Task Claims_CreateWithMissingPolicy_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/claims",
            new ClaimCreateDto(999999, GetBusinessDate(), "台北市", "找不到保單", 1000m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Claims_CreateWithAccidentDateOutsidePolicy_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateIssuedPolicyAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/claims",
            new ClaimCreateDto(policy.Id, GetBusinessDate(), "台北市", "事故日在保期外", 1000m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Claims_CreateWithFutureAccidentDate_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateIssuedPolicyAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/claims",
            new ClaimCreateDto(policy.Id, GetBusinessDate().AddDays(1), "台北市", "未來事故日", 1000m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Claims_CreateGetFilterAndUpdate_FollowsContract()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreateClaimablePolicyAsync(factory);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/claims",
            CreateClaim(state.Policy.Id, state.AccidentDate));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(created);
        Assert.Equal("pending_intake", created.Status);
        Assert.Equal("claim_created", created.LastTransition);
        Assert.StartsWith("CLM-", created.ClaimNumber);
        Assert.Equal(state.Policy.Id, created.PolicyId);

        var byIdResponse = await client.GetAsync($"/api/claims/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, byIdResponse.StatusCode);

        var filterResponse = await client.GetAsync($"/api/claims?status=pending_intake&policyId={state.Policy.Id}");
        Assert.Equal(HttpStatusCode.OK, filterResponse.StatusCode);
        var filtered = await filterResponse.Content.ReadFromJsonAsync<List<ClaimDto>>();
        Assert.NotNull(filtered);
        Assert.Contains(filtered, x => x.Id == created.Id);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/claims/{created.Id}",
            new ClaimUpdateDto(
                state.AccidentDate,
                "新北市板橋區",
                "更新事故描述",
                12000m));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(updated);
        Assert.Equal("新北市板橋區", updated.AccidentLocation);
        Assert.Equal("更新事故描述", updated.AccidentDescription);
        Assert.Equal(12000m, updated.ClaimedAmount);

        var invalidStatus = await client.GetAsync("/api/claims?status=not-a-status");
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
    }

    [Fact]
    public async Task Claims_TransitionReviewDocumentsAndBack_FollowsAllowedStateMachine()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreateClaimablePolicyAsync(factory);
        using var client = factory.CreateClient();
        var claim = await CreateClaimAsync(client, state.Policy.Id, state.AccidentDate);

        var reviewResponse = await client.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/transition",
            new ClaimTransitionDto("reviewing", "理賠員-A", "開始審查"));
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var reviewing = await reviewResponse.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(reviewing);
        Assert.Equal("reviewing", reviewing.Status);
        Assert.Equal("理賠員-A", reviewing.AssignedAdjuster);
        Assert.Equal("claim_reviewing", reviewing.LastTransition);

        var requestDocuments = await client.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/transition",
            new ClaimTransitionDto("pending_documents", null, "補事故照片"));
        Assert.Equal(HttpStatusCode.OK, requestDocuments.StatusCode);
        var pendingDocuments = await requestDocuments.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(pendingDocuments);
        Assert.Equal("pending_documents", pendingDocuments.Status);
        Assert.Equal("claim_documents_requested", pendingDocuments.LastTransition);

        var backToReview = await client.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/transition",
            new ClaimTransitionDto("reviewing", "理賠員-B", "文件已補齊"));
        Assert.Equal(HttpStatusCode.OK, backToReview.StatusCode);
        var reviewedAgain = await backToReview.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(reviewedAgain);
        Assert.Equal("reviewing", reviewedAgain.Status);
        Assert.Equal("理賠員-B", reviewedAgain.AssignedAdjuster);
    }

    [Fact]
    public async Task Claims_IllegalTransition_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreateClaimablePolicyAsync(factory);
        using var client = factory.CreateClient();
        var claim = await CreateClaimAsync(client, state.Policy.Id, state.AccidentDate);

        var response = await client.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/transition",
            new ClaimTransitionDto("pending_documents", null, "非法跳階"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Claims_DeletePendingIntakeButNotReviewing_FollowsStatusGuard()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreateClaimablePolicyAsync(factory);
        using var client = factory.CreateClient();

        var draft = await CreateClaimAsync(client, state.Policy.Id, state.AccidentDate);
        var deleteDraft = await client.DeleteAsync($"/api/claims/{draft.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteDraft.StatusCode);

        var reviewing = await CreateClaimAsync(client, state.Policy.Id, state.AccidentDate);
        (await client.PostAsJsonAsync(
            $"/api/claims/{reviewing.Id}/transition",
            new ClaimTransitionDto("reviewing", "理賠員", null))).EnsureSuccessStatusCode();

        var deleteReviewing = await client.DeleteAsync($"/api/claims/{reviewing.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteReviewing.StatusCode);
    }

    [Fact]
    public async Task Claims_LiabilityWithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/claims/1/liability",
            new ClaimLiabilityDto("有責", 100, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Claims_LiabilityAsSales_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await sales.PostAsJsonAsync(
            "/api/claims/1/liability",
            new ClaimLiabilityDto("有責", 100, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Claims_SettlementFlow_ClosesClaimWritesAuditsAndRaisesClaimRecordLevel()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreateClaimablePolicyAsync(factory);
        using var client = factory.CreateClient();
        var claim = await CreateClaimAsync(client, state.Policy.Id, state.AccidentDate, 10000m);
        await MoveToReviewingAsync(client, claim.Id);
        using var claims = await TestAuth.CreateAuthenticatedClientAsync(factory, "claims");

        int beforeLevel;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            beforeLevel = await db.Customers
                .Where(x => x.Id == state.Policy.CustomerId)
                .Select(x => x.ClaimRecordLevel)
                .SingleAsync();
        }

        var liabilityResponse = await claims.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/liability",
            new ClaimLiabilityDto("被保險人負 70% 責任", 70, "完成責任認定"));
        Assert.Equal(HttpStatusCode.OK, liabilityResponse.StatusCode);
        var calculating = await liabilityResponse.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(calculating);
        Assert.Equal("calculating", calculating.Status);
        Assert.Equal(70, calculating.LiabilityPercentage);
        Assert.Equal("claim_calculating", calculating.LastTransition);

        var overClaimed = await claims.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/approve",
            new ClaimApproveDto(10001m, "超過請求金額"));
        Assert.Equal(HttpStatusCode.BadRequest, overClaimed.StatusCode);

        var approveResponse = await claims.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/approve",
            new ClaimApproveDto(7000m, "核定七成"));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var pendingPayment = await approveResponse.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(pendingPayment);
        Assert.Equal("pending_payment", pendingPayment.Status);
        Assert.Equal(7000m, pendingPayment.ApprovedAmount);
        Assert.Equal("claim_payment_pending", pendingPayment.LastTransition);

        var payResponse = await claims.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/pay",
            new ClaimPayDto("bank_transfer", "完成給付"));
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);
        var closed = await payResponse.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(closed);
        Assert.Equal("closed", closed.Status);
        Assert.Equal("bank_transfer", closed.PaymentMethod);
        Assert.NotNull(closed.PaidAtUtc);
        Assert.Equal("claim_closed", closed.LastTransition);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            var afterLevel = await db.Customers
                .Where(x => x.Id == state.Policy.CustomerId)
                .Select(x => x.ClaimRecordLevel)
                .SingleAsync();
            Assert.Equal(Math.Min(10, beforeLevel + 3), afterLevel);

            var objectId = claim.Id.ToString();
            Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "claim.liability.completed" && x.ObjectId == objectId));
            Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "claim.amount.approved" && x.ObjectId == objectId));
            Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "claim.payment.completed" && x.ObjectId == objectId));
        }

        var updateClosed = await client.PutAsJsonAsync(
            $"/api/claims/{claim.Id}",
            new ClaimUpdateDto(state.AccidentDate, "台北市", "結案後修改", 10000m));
        Assert.Equal(HttpStatusCode.Conflict, updateClosed.StatusCode);

        var transitionClosed = await client.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/transition",
            new ClaimTransitionDto("reviewing", "理賠員", null));
        Assert.Equal(HttpStatusCode.Conflict, transitionClosed.StatusCode);
    }

    [Fact]
    public async Task Claims_ManagerCanDenyAndDeniedClaimIsTerminal()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreateClaimablePolicyAsync(factory);
        using var client = factory.CreateClient();
        var claim = await CreateClaimAsync(client, state.Policy.Id, state.AccidentDate);
        await MoveToReviewingAsync(client, claim.Id);
        using var manager = await TestAuth.CreateAuthenticatedClientAsync(factory, "manager");

        var denyResponse = await manager.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/deny",
            new ClaimDenyDto("不在承保事故範圍"));

        Assert.Equal(HttpStatusCode.OK, denyResponse.StatusCode);
        var denied = await denyResponse.Content.ReadFromJsonAsync<ClaimDto>();
        Assert.NotNull(denied);
        Assert.Equal("denied", denied.Status);
        Assert.Equal("不在承保事故範圍", denied.DeniedReason);
        Assert.Equal("claim_denied", denied.LastTransition);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            Assert.True(await db.AuditLogs.AnyAsync(x =>
                x.Action == "claim.denied" && x.ObjectId == claim.Id.ToString()));
        }

        var updateDenied = await client.PutAsJsonAsync(
            $"/api/claims/{claim.Id}",
            new ClaimUpdateDto(state.AccidentDate, "台北市", "拒賠後修改", 10000m));
        Assert.Equal(HttpStatusCode.Conflict, updateDenied.StatusCode);

        var transitionDenied = await client.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/transition",
            new ClaimTransitionDto("reviewing", "理賠員", null));
        Assert.Equal(HttpStatusCode.Conflict, transitionDenied.StatusCode);
    }

    private static ClaimCreateDto CreateClaim(int policyId, DateOnly accidentDate, decimal claimedAmount = 10000m) =>
        new(policyId, accidentDate, "台北市信義區", "T-050H 整合測試事故", claimedAmount);

    private static async Task<ClaimDto> CreateClaimAsync(
        HttpClient client,
        int policyId,
        DateOnly accidentDate,
        decimal claimedAmount = 10000m)
    {
        var response = await client.PostAsJsonAsync(
            "/api/claims",
            CreateClaim(policyId, accidentDate, claimedAmount));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ClaimDto>()
            ?? throw new InvalidOperationException("Create claim 未回傳 DTO。");
    }

    private static async Task MoveToReviewingAsync(HttpClient client, int claimId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/claims/{claimId}/transition",
            new ClaimTransitionDto("reviewing", "T-050H 理賠員", "開始審查"));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<(PolicyDto Policy, DateOnly AccidentDate)> CreateClaimablePolicyAsync(
        CarInsuranceApiFactory factory)
    {
        var policy = await CreateIssuedPolicyAsync(factory);
        var today = GetBusinessDate();
        var startDate = today.AddDays(-30);
        var endDate = today.AddDays(335);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        var entity = await db.Policies.SingleAsync(x => x.Id == policy.Id);
        entity.InsuranceStartDate = ToUtcStartOfBusinessDate(startDate);
        entity.InsuranceEndDate = ToUtcStartOfBusinessDate(endDate);
        await db.SaveChangesAsync();

        return (policy, today);
    }

    private static DateTime ToUtcStartOfBusinessDate(DateOnly date)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        return TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            tz);
    }

    private static DateOnly GetBusinessDate()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
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
            new PaymentConfirmDto("T050H-CONFIRM", "T-050H 整合測試"));
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
            new UnderwritingDecisionRequestDto { Opinion = "T-050H 整合測試核准" });
        approve.EnsureSuccessStatusCode();
        return caseId;
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
                GetBusinessDate().AddDays(1),
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
