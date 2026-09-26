using System.Data;
using System.Text;
using CarInsurance.Api.Authorization;
using CarInsurance.Api.Data;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("缺少必要設定 Jwt:Issuer。");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("缺少必要設定 Jwt:Audience。");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("缺少必要設定 Jwt:SigningKey。");
if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey 至少需要 32 bytes。");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.FindFirst("token_type")?.Value != "access")
                {
                    context.Fail("Only access tokens are accepted.");
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddCarInsuranceAuthorization();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementRepository, UserManagementRepository>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuditLogQueryRepository, AuditLogQueryRepository>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IInsuranceCatalogRepository, InsuranceCatalogRepository>();
builder.Services.AddScoped<IInsuranceCatalogService, InsuranceCatalogService>();
builder.Services.AddScoped<IRateTableRepository, RateTableRepository>();
builder.Services.AddScoped<IRateTableService, RateTableService>();
builder.Services.AddScoped<IPremiumCalculationService, PremiumCalculationService>();
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IApplicationDocumentStorage, FileSystemApplicationDocumentStorage>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IUnderwritingRepository, UnderwritingRepository>();
builder.Services.AddScoped<IUnderwritingService, UnderwritingService>();
builder.Services.AddScoped<IUnderwritingQueueRepository, UnderwritingQueueRepository>();
builder.Services.AddScoped<IUnderwritingQueueService, UnderwritingQueueService>();
builder.Services.AddScoped<IPolicyLifecycleRepository, PolicyLifecycleRepository>();
builder.Services.AddScoped<IPolicyLifecycleService, PolicyLifecycleService>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
builder.Services.AddScoped<IPolicyPdfGenerator, PolicyPdfGenerator>();
builder.Services.AddScoped<IPolicyDocumentStorage, FileSystemPolicyDocumentStorage>();
builder.Services.AddScoped<IPolicyService, PolicyService>();
builder.Services.AddScoped<IRenewalRepository, RenewalRepository>();
builder.Services.AddScoped<IRenewalService, RenewalService>();
builder.Services.AddScoped<IRenewalReminderRepository, RenewalReminderRepository>();
builder.Services.AddScoped<IRenewalReminderService, RenewalReminderService>();
builder.Services.AddHostedService<RenewalReminderBackgroundService>();
builder.Services.AddScoped<IPostIssuanceRepository, PostIssuanceRepository>();
builder.Services.AddScoped<IEndorsementRepository, EndorsementRepository>();
builder.Services.AddScoped<IEndorsementService, EndorsementService>();
builder.Services.AddScoped<IClaimRepository, ClaimRepository>();
builder.Services.AddScoped<IClaimService, ClaimService>();
builder.Services.AddScoped<IDashboardSummaryRepository, DashboardSummaryRepository>();
builder.Services.AddScoped<IDashboardSummaryService, DashboardSummaryService>();
builder.Services.AddScoped<IDocumentManagementRepository, DocumentManagementRepository>();
builder.Services.AddScoped<IDocumentManagementService, DocumentManagementService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPostIssuanceService, PostIssuanceService>();
builder.Services.AddDbContext<CarInsuranceDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// 開發階段前端跑在 Vite dev server，正式佈署時同源（nginx 反向代理），不需要 CORS
const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    await using (var pragmaCommand = connection.CreateCommand())
    {
        pragmaCommand.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        await pragmaCommand.ExecuteNonQueryAsync();
    }

    await dbContext.Database.MigrateAsync();
    await SeedData.InitializeAsync(dbContext, builder.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
}

// ponytail: yagni: 不做 HTTPS 轉址 | upgrade if: API 直接對外，屆時憑證由 nginx 或反向代理處理
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
