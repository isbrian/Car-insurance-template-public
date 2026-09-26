using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.PaymentPolicy;

public sealed class PaymentPolicyIntegrationTests
{
    private const string SignaturePngDataUrl = "data:image/png;base64,iVBORw0KGgo=";

    [Fact]
    public async Task Payments_CreateBeforeUnderwritingApproval_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales);

        var response = await sales.PostAsJsonAsync("/api/payments", CreatePayment(application.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Payments_CreateWithMissingApplication_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments", CreatePayment(999999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Payments_CreateWithInvalidMethod_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreatePendingPaymentApplicationAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/payments",
            new PaymentCreateDto(state.Application.Id, "annual", "crypto"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payments_CreateGetUpdateAndDuplicate_FollowsContract()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreatePendingPaymentApplicationAsync(factory);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/payments", CreatePayment(state.Application.Id));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(created);
        Assert.Equal("pending", created.Status);
        Assert.Equal("annual", created.PaymentFrequency);
        Assert.Equal("credit_card", created.PaymentMethod);
        Assert.Equal(1, created.InstallmentCount);
        Assert.Equal("pending_payment", created.PolicyLifecycleStatus);
        Assert.True(created.AmountDue > 0);

        var byId = await client.GetAsync($"/api/payments/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        var byApplication = await client.GetAsync($"/api/payments/by-application/{state.Application.Id}");
        Assert.Equal(HttpStatusCode.OK, byApplication.StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/payments/{created.Id}",
            new PaymentUpdateDto("quarterly", "atm", "整合測試更新"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(updated);
        Assert.Equal("quarterly", updated.PaymentFrequency);
        Assert.Equal("atm", updated.PaymentMethod);
        Assert.Equal(4, updated.InstallmentCount);
        Assert.Equal("整合測試更新", updated.Note);

        var duplicate = await client.PostAsJsonAsync("/api/payments", CreatePayment(state.Application.Id));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Payments_ConfirmWithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/payments/1/confirm",
            new PaymentConfirmDto("REF", "test"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Payments_ConfirmAsSales_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await sales.PostAsJsonAsync(
            "/api/payments/1/confirm",
            new PaymentConfirmDto("REF", "test"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Payments_ConfirmAsUnderwriter_MarksPaidMovesLifecycleAndWritesAudit()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreatePendingPaymentApplicationAsync(factory);
        using var client = factory.CreateClient();
        var payment = await CreatePaymentAsync(client, state.Application.Id);
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");

        var response = await underwriter.PostAsJsonAsync(
            $"/api/payments/{payment.Id}/confirm",
            new PaymentConfirmDto("TXN-T050F", "整合測試收款"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paid = await response.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(paid);
        Assert.Equal("paid", paid.Status);
        Assert.Equal("issued", paid.PolicyLifecycleStatus);
        Assert.Equal("TXN-T050F", paid.ReferenceNumber);
        Assert.NotNull(paid.PaidAtUtc);

        var lifecycleResponse = await client.GetAsync($"/api/policy-lifecycles/by-application/{state.Application.Id}");
        Assert.Equal(HttpStatusCode.OK, lifecycleResponse.StatusCode);
        var lifecycle = await lifecycleResponse.Content.ReadFromJsonAsync<PolicyLifecycleDto>();
        Assert.NotNull(lifecycle);
        Assert.Equal("issued", lifecycle.Status);
        Assert.Equal("payment_confirmed", lifecycle.LastTransition);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x =>
            x.Action == "payment.confirmed" && x.ObjectId == payment.Id.ToString()));

        var second = await underwriter.PostAsJsonAsync(
            $"/api/payments/{payment.Id}/confirm",
            new PaymentConfirmDto("SECOND", null));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Payments_MarkOverdueBeforeDueDate_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreatePendingPaymentApplicationAsync(factory);
        using var client = factory.CreateClient();
        var payment = await CreatePaymentAsync(client, state.Application.Id);

        var response = await client.PostAsync($"/api/payments/{payment.Id}/mark-overdue", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Payments_MarkOverdueAfterDueDate_CancelsLifecycle()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreatePendingPaymentApplicationAsync(factory);
        using var client = factory.CreateClient();
        var payment = await CreatePaymentAsync(client, state.Application.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            var entity = await db.Payments.SingleAsync(x => x.Id == payment.Id);
            entity.DueAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsync($"/api/payments/{payment.Id}/mark-overdue", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var overdue = await response.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(overdue);
        Assert.Equal("overdue", overdue.Status);
        Assert.Equal("cancelled", overdue.PolicyLifecycleStatus);
        Assert.NotNull(overdue.CancelledAtUtc);
    }

    [Fact]
    public async Task PolicyLifecycle_SubmissionApprovalAndPaymentConfirmation_ProgressesStatuses()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales);
        using var client = factory.CreateClient();

        var submittedLifecycle = await GetLifecycleByApplicationAsync(client, application.Id);
        Assert.Equal("pending_underwriting", submittedLifecycle.Status);

        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");
        await ApproveUnderwritingAsync(sales, underwriter, application.Id);
        var approvedLifecycle = await GetLifecycleByApplicationAsync(client, application.Id);
        Assert.Equal("pending_payment", approvedLifecycle.Status);

        var payment = await CreatePaymentAsync(client, application.Id);
        await ConfirmPaymentAsync(underwriter, payment.Id);
        var paidLifecycle = await GetLifecycleByApplicationAsync(client, application.Id);
        Assert.Equal("issued", paidLifecycle.Status);
    }

    [Fact]
    public async Task PolicyLifecycle_UnsupportedTarget_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales);
        using var client = factory.CreateClient();
        var lifecycle = await GetLifecycleByApplicationAsync(client, application.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/policy-lifecycles/{lifecycle.Id}/transition",
            new PolicyLifecycleTransitionDto("not-a-status", "integration_test", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PolicyLifecycle_IllegalTransition_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var application = await CreateSubmittedApplicationAsync(factory, sales);
        using var client = factory.CreateClient();
        var lifecycle = await GetLifecycleByApplicationAsync(client, application.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/policy-lifecycles/{lifecycle.Id}/transition",
            new PolicyLifecycleTransitionDto("active", "skip_states", "非法跳階測試"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PolicyLifecycle_GetMissing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/policy-lifecycles/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Policies_IssueWithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/policies/issue/1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Policies_IssueAsSales_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await sales.PostAsync("/api/policies/issue/1", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Policies_IssueBeforePaymentConfirmed_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreatePendingPaymentApplicationAsync(factory);
        using var client = factory.CreateClient();
        _ = await CreatePaymentAsync(client, state.Application.Id);
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");

        var response = await underwriter.PostAsync($"/api/policies/issue/{state.Application.Id}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Policies_FullIssueFlow_CreatesPolicyPdfTimelineAuditAndRejectsDuplicate()
    {
        using var factory = new CarInsuranceApiFactory();
        var state = await CreatePendingPaymentApplicationAsync(factory);
        using var client = factory.CreateClient();
        var payment = await CreatePaymentAsync(client, state.Application.Id);
        using var underwriter = await TestAuth.CreateAuthenticatedClientAsync(factory, "underwriter");
        _ = await ConfirmPaymentAsync(underwriter, payment.Id);

        var issueResponse = await underwriter.PostAsync($"/api/policies/issue/{state.Application.Id}", null);
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var policy = await issueResponse.Content.ReadFromJsonAsync<PolicyDto>();
        Assert.NotNull(policy);
        Assert.Equal("issued", policy.Status);
        Assert.StartsWith("POL-", policy.PolicyNumber);
        Assert.Equal(state.Application.Id, policy.ApplicationId);
        Assert.Equal(payment.Id, policy.PaymentId);
        Assert.True(policy.PdfSizeBytes > 0);
        Assert.False(string.IsNullOrWhiteSpace(policy.PdfSha256));

        var detailsResponse = await client.GetAsync($"/api/policies/{policy.Id}");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        var details = await detailsResponse.Content.ReadFromJsonAsync<PolicyDetailsDto>();
        Assert.NotNull(details);
        Assert.Equal(policy.PolicyNumber, details.PolicyNumber);

        var byApplication = await client.GetAsync($"/api/policies/by-application/{state.Application.Id}");
        Assert.Equal(HttpStatusCode.OK, byApplication.StatusCode);

        var search = await client.GetAsync($"/api/policies?keyword={policy.PolicyNumber}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var searchResult = await search.Content.ReadFromJsonAsync<PolicyListResponse>();
        Assert.NotNull(searchResult);
        Assert.Contains(searchResult.Items, x => x.Id == policy.Id);

        var timelineResponse = await client.GetAsync($"/api/policies/{policy.Id}/timeline");
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        var timeline = await timelineResponse.Content.ReadFromJsonAsync<PolicyTimelineDto>();
        Assert.NotNull(timeline);
        Assert.Contains(timeline.Events, x => x.Key == "application_submitted");
        Assert.Contains(timeline.Events, x => x.Key == "payment_created");
        Assert.Contains(timeline.Events, x => x.Key == "payment_paid");
        Assert.Contains(timeline.Events, x => x.Key == "policy_issued");

        var documentResponse = await client.GetAsync($"/api/policies/{policy.Id}/document");
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal("application/pdf", documentResponse.Content.Headers.ContentType?.MediaType);
        var pdf = await documentResponse.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(pdf);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            Assert.True(await db.AuditLogs.AnyAsync(x =>
                x.Action == "policy.issued" && x.ObjectId == policy.Id.ToString()));
        }

        var duplicate = await underwriter.PostAsync($"/api/policies/issue/{state.Application.Id}", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Policies_InvalidPagination_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/policies?page=0&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Policies_GetMissing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/policies/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/policies/999999/document")).StatusCode);
    }

    [Fact]
    public async Task PostIssuance_ProcessIssuedPolicy_CompletesThreeStepsAndIsIdempotent()
    {
        using var factory = new CarInsuranceApiFactory();
        var policy = await CreateIssuedPolicyAsync(factory);
        using var client = factory.CreateClient();

        var processResponse = await client.PostAsync($"/api/policies/{policy.Id}/post-issuance", null);
        Assert.Equal(HttpStatusCode.OK, processResponse.StatusCode);
        var processed = await processResponse.Content.ReadFromJsonAsync<PolicyPostIssuanceTimelineDto>();
        Assert.NotNull(processed);
        Assert.Equal(3, processed.Items.Count);
        Assert.Collection(
            processed.Items,
            x => { Assert.Equal("bureau_upload", x.Step); Assert.Equal("completed", x.Status); Assert.Equal(1, x.AttemptCount); },
            x => { Assert.Equal("e_policy_delivery", x.Step); Assert.Equal("completed", x.Status); Assert.Equal(1, x.AttemptCount); },
            x => { Assert.Equal("commission_sync", x.Step); Assert.Equal("completed", x.Status); Assert.Equal(1, x.AttemptCount); });

        var getResponse = await client.GetAsync($"/api/policies/{policy.Id}/post-issuance");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var timeline = await getResponse.Content.ReadFromJsonAsync<PolicyPostIssuanceTimelineDto>();
        Assert.NotNull(timeline);
        Assert.Equal(3, timeline.Items.Count);

        var secondResponse = await client.PostAsync($"/api/policies/{policy.Id}/post-issuance", null);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var second = await secondResponse.Content.ReadFromJsonAsync<PolicyPostIssuanceTimelineDto>();
        Assert.NotNull(second);
        Assert.All(second.Items, x => Assert.Equal(1, x.AttemptCount));
    }

    [Fact]
    public async Task PostIssuance_ProcessMissingPolicy_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/policies/999999/post-issuance", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            new PaymentConfirmDto("T050F-CONFIRM", "整合測試"));
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
            new UnderwritingDecisionRequestDto { Opinion = "T-050F 整合測試核准" });
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
