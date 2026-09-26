using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.DocumentNotificationAudit;

public sealed class DocumentNotificationAuditIntegrationTests
{
    [Fact]
    public async Task Documents_CasesQueryAndFilters_ReturnUploadedDocument()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var quote = await CreateQuoteAsync(factory, sales);
        var application = await CreateApplicationAsync(sales, quote.Id);
        await UploadVehicleRegistrationAsync(sales, application.Id);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/documents/cases?status=uploaded&documentType=vehicle_registration&keyword={Uri.EscapeDataString(application.ApplicationNumber)}&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CaseDocumentListResponse>();
        Assert.NotNull(result);
        var item = Assert.Single(result.Items, x => x.ApplicationId == application.Id);
        Assert.Equal(application.ApplicationNumber, item.ApplicationNumber);
        Assert.Equal(quote.CustomerId, item.CustomerId);
        Assert.Equal(quote.VehicleId, item.VehicleId);
        Assert.Equal("vehicle_registration", item.DocumentType);
        Assert.Equal("uploaded", item.Status);
        Assert.Equal("vehicle-registration.pdf", item.OriginalFileName);
        Assert.True(item.SizeBytes > 0);
        Assert.Equal(64, item.Sha256.Length);
    }

    [Fact]
    public async Task Documents_InvalidQueryGuards_ReturnBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/documents/cases?status=invalid")).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/documents/cases?page=0")).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/documents/cases?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task DocumentTemplates_CreateGetListAddVersionsAndActivate_FollowsContract()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/document-templates",
            new DocumentTemplateCreateDto("T050I-DEMO", "T-050I 範本", "整合測試"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<DocumentTemplateDetailDto>();
        Assert.NotNull(created);
        Assert.Equal("t050i-demo", created.Code);
        Assert.Empty(created.Versions);
        Assert.Null(created.ActiveVersionId);

        var getResponse = await client.GetAsync($"/api/document-templates/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/document-templates");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<List<DocumentTemplateSummaryDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, x => x.Id == created.Id && x.Code == "t050i-demo");

        var version1Response = await client.PostAsJsonAsync(
            $"/api/document-templates/{created.Id}/versions",
            new DocumentTemplateVersionCreateDto("v1", "version-one", false));
        Assert.Equal(HttpStatusCode.Created, version1Response.StatusCode);
        var version1Detail = await version1Response.Content.ReadFromJsonAsync<DocumentTemplateDetailDto>();
        Assert.NotNull(version1Detail);
        var version1 = Assert.Single(version1Detail.Versions, x => x.Version == "v1");
        Assert.Equal(64, version1.Sha256.Length);
        Assert.Null(version1Detail.ActiveVersionId);

        var version2Response = await client.PostAsJsonAsync(
            $"/api/document-templates/{created.Id}/versions",
            new DocumentTemplateVersionCreateDto("v2", "version-two", true));
        Assert.Equal(HttpStatusCode.Created, version2Response.StatusCode);
        var version2Detail = await version2Response.Content.ReadFromJsonAsync<DocumentTemplateDetailDto>();
        Assert.NotNull(version2Detail);
        var version2 = Assert.Single(version2Detail.Versions, x => x.Version == "v2");
        Assert.Equal(version2.Id, version2Detail.ActiveVersionId);
        Assert.Equal("v2", version2Detail.ActiveVersion);

        var activateV1 = await client.PutAsJsonAsync(
            $"/api/document-templates/{created.Id}/active-version",
            new DocumentTemplateActivateDto(version1.Id));
        Assert.Equal(HttpStatusCode.OK, activateV1.StatusCode);
        var activated = await activateV1.Content.ReadFromJsonAsync<DocumentTemplateDetailDto>();
        Assert.NotNull(activated);
        Assert.Equal(version1.Id, activated.ActiveVersionId);
        Assert.Equal("v1", activated.ActiveVersion);
    }

    [Fact]
    public async Task DocumentTemplates_DuplicateCodeAndVersion_ReturnConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var first = await CreateTemplateAsync(client, "t050i-dup", "重複測試");

        var duplicateCode = await client.PostAsJsonAsync(
            "/api/document-templates",
            new DocumentTemplateCreateDto("T050I-DUP", "另一個名稱", null));
        Assert.Equal(HttpStatusCode.Conflict, duplicateCode.StatusCode);

        var firstVersion = await client.PostAsJsonAsync(
            $"/api/document-templates/{first.Id}/versions",
            new DocumentTemplateVersionCreateDto("v1", "one", false));
        Assert.Equal(HttpStatusCode.Created, firstVersion.StatusCode);

        var duplicateVersion = await client.PostAsJsonAsync(
            $"/api/document-templates/{first.Id}/versions",
            new DocumentTemplateVersionCreateDto("v1", "two", false));
        Assert.Equal(HttpStatusCode.Conflict, duplicateVersion.StatusCode);
    }

    [Fact]
    public async Task DocumentTemplates_MissingAndForeignVersion_ReturnExpectedErrors()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync("/api/document-templates/999999")).StatusCode);

        var left = await CreateTemplateAsync(client, "t050i-left", "左側範本");
        var right = await CreateTemplateAsync(client, "t050i-right", "右側範本");
        var rightVersionResponse = await client.PostAsJsonAsync(
            $"/api/document-templates/{right.Id}/versions",
            new DocumentTemplateVersionCreateDto("v1", "right", false));
        rightVersionResponse.EnsureSuccessStatusCode();
        var rightDetail = await rightVersionResponse.Content.ReadFromJsonAsync<DocumentTemplateDetailDto>();
        Assert.NotNull(rightDetail);
        var foreignVersionId = Assert.Single(rightDetail.Versions).Id;

        var foreign = await client.PutAsJsonAsync(
            $"/api/document-templates/{left.Id}/active-version",
            new DocumentTemplateActivateDto(foreignVersionId));
        Assert.Equal(HttpStatusCode.Conflict, foreign.StatusCode);

        var missing = await client.PutAsJsonAsync(
            "/api/document-templates/999999/active-version",
            new DocumentTemplateActivateDto(foreignVersionId));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Notifications_InAppCreateAndRead_WritesAudit()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/notifications",
            new NotificationCreateDto(
                "t050i.in_app",
                "in_app",
                "user-1",
                "站內通知",
                "T-050I message",
                "Test",
                "1"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<NotificationDto>();
        Assert.NotNull(created);
        Assert.Equal("sent", created.Status);
        Assert.Equal(1, created.AttemptCount);
        Assert.NotNull(created.SentAtUtc);
        Assert.Null(created.ReadAtUtc);

        var readResponse = await client.PostAsync($"/api/notifications/{created.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var read = await readResponse.Content.ReadFromJsonAsync<NotificationDto>();
        Assert.NotNull(read);
        Assert.NotNull(read.ReadAtUtc);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        var audit = await db.AuditLogs.SingleAsync(x =>
            x.Action == "notification.read" && x.ObjectId == created.Id.ToString());
        Assert.Equal("Notification", audit.ObjectType);
        Assert.Equal("anonymous", audit.Actor);
        Assert.Equal("anonymous", audit.Role);
    }

    [Fact]
    public async Task Notifications_FailedExternalRetrySuccess_WritesAudit()
    {
        using var factory = new CarInsuranceApiFactory();
        using var manager = await TestAuth.CreateAuthenticatedClientAsync(factory, "manager");

        var createResponse = await manager.PostAsJsonAsync(
            "/api/notifications",
            new NotificationCreateDto(
                "t050i.external",
                "email",
                "fail-demo:test@example.com",
                "外部通知",
                "T-050I external",
                "Policy",
                "P-1"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var failed = await createResponse.Content.ReadFromJsonAsync<NotificationDto>();
        Assert.NotNull(failed);
        Assert.Equal("failed", failed.Status);
        Assert.Equal(1, failed.AttemptCount);
        Assert.NotNull(failed.LastError);
        Assert.Null(failed.SentAtUtc);

        var retryResponse = await manager.PostAsJsonAsync(
            $"/api/notifications/{failed.Id}/retry",
            new NotificationRetryDto("ok@example.com"));
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var sent = await retryResponse.Content.ReadFromJsonAsync<NotificationDto>();
        Assert.NotNull(sent);
        Assert.Equal("sent", sent.Status);
        Assert.Equal(2, sent.AttemptCount);
        Assert.Null(sent.LastError);
        Assert.NotNull(sent.SentAtUtc);
        Assert.Equal("ok@example.com", sent.Recipient);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x =>
            x.Action == "notification.retried" &&
            x.ObjectId == failed.Id.ToString() &&
            x.ObjectType == "Notification"));
    }

    [Fact]
    public async Task Notifications_ValidationGuards_ReturnBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var invalidChannel = await client.PostAsJsonAsync(
            "/api/notifications",
            new NotificationCreateDto(
                "t050i.invalid",
                "fax",
                "someone",
                "invalid",
                "invalid",
                null,
                null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidChannel.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/notifications?status=invalid")).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/notifications?page=0")).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/notifications?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task Notifications_RetryAndReadConflictGuards_ReturnConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var inApp = await CreateNotificationAsync(
            client,
            new NotificationCreateDto("t050i.inapp.conflict", "in_app", "user-2", "title", "message", null, null));
        var retryInApp = await client.PostAsJsonAsync(
            $"/api/notifications/{inApp.Id}/retry",
            new NotificationRetryDto(null));
        Assert.Equal(HttpStatusCode.Conflict, retryInApp.StatusCode);

        var email = await CreateNotificationAsync(
            client,
            new NotificationCreateDto("t050i.email.sent", "email", "ok@example.com", "title", "message", null, null));
        Assert.Equal("sent", email.Status);

        var retrySent = await client.PostAsJsonAsync(
            $"/api/notifications/{email.Id}/retry",
            new NotificationRetryDto(null));
        Assert.Equal(HttpStatusCode.Conflict, retrySent.StatusCode);

        var readExternal = await client.PostAsync($"/api/notifications/{email.Id}/read", null);
        Assert.Equal(HttpStatusCode.Conflict, readExternal.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_Sales_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var sales = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await sales.GetAsync("/api/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_AdminAndManagerCanQueryRealNotificationAudit_WithFiltersAndValidation()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var notification = await CreateNotificationAsync(
            client,
            new NotificationCreateDto(
                "t050i.audit",
                "in_app",
                "audit-user",
                "audit title",
                "audit message",
                "TestRef",
                "A-1"));
        var markRead = await client.PostAsync($"/api/notifications/{notification.Id}/read", null);
        markRead.EnsureSuccessStatusCode();

        using var admin = await TestAuth.CreateAuthenticatedClientAsync(factory, "admin");
        var adminResponse = await admin.GetAsync(
            $"/api/audit-logs?action=notification.read&objectType=Notification&objectId={notification.Id}&actor=anonymous&role=anonymous&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var adminResult = await adminResponse.Content.ReadFromJsonAsync<AuditLogListResponseDto>();
        Assert.NotNull(adminResult);
        var audit = Assert.Single(adminResult.Items, x =>
            x.Action == "notification.read" && x.ObjectId == notification.Id.ToString());
        Assert.Equal("Notification", audit.ObjectType);
        Assert.Equal("anonymous", audit.Actor);
        Assert.Equal("anonymous", audit.Role);
        Assert.Equal(1, adminResult.Page);
        Assert.Equal(20, adminResult.PageSize);
        Assert.True(adminResult.Total >= 1);

        using var manager = await TestAuth.CreateAuthenticatedClientAsync(factory, "manager");
        var managerResponse = await manager.GetAsync(
            $"/api/audit-logs?action=notification.read&objectId={notification.Id}");
        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);

        var invalidRange = await admin.GetAsync(
            "/api/audit-logs?fromUtc=2026-09-22T00:00:00Z&toUtc=2026-09-21T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);
    }

    private static async Task<DocumentTemplateDetailDto> CreateTemplateAsync(
        HttpClient client,
        string code,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/document-templates",
            new DocumentTemplateCreateDto(code, name, null));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentTemplateDetailDto>()
            ?? throw new InvalidOperationException("Create document template 未回傳 DTO。");
    }

    private static async Task<NotificationDto> CreateNotificationAsync(
        HttpClient client,
        NotificationCreateDto dto)
    {
        var response = await client.PostAsJsonAsync("/api/notifications", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotificationDto>()
            ?? throw new InvalidOperationException("Create notification 未回傳 DTO。");
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
                "T-050I 整合測試業務",
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
        using var file = new ByteArrayContent("t050i-vehicle-registration"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipart.Add(file, "file", "vehicle-registration.pdf");

        var response = await client.PostAsync(
            $"/api/applications/{applicationId}/documents/vehicle_registration",
            multipart);
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
