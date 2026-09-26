using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Data;

public class CarInsuranceDbContext(DbContextOptions<CarInsuranceDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<InsuranceType> InsuranceTypes => Set<InsuranceType>();

    public DbSet<InsuranceProduct> InsuranceProducts => Set<InsuranceProduct>();

    public DbSet<RateVersion> RateVersions => Set<RateVersion>();

    public DbSet<RateTable> RateTables => Set<RateTable>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<InsuranceApplication> Applications => Set<InsuranceApplication>();

    public DbSet<ApplicationBeneficiary> ApplicationBeneficiaries => Set<ApplicationBeneficiary>();

    public DbSet<ApplicationSignature> ApplicationSignatures => Set<ApplicationSignature>();

    public DbSet<ApplicationDocument> ApplicationDocuments => Set<ApplicationDocument>();

    public DbSet<UnderwritingRule> UnderwritingRules => Set<UnderwritingRule>();

    public DbSet<UnderwritingResult> UnderwritingResults => Set<UnderwritingResult>();

    public DbSet<UnderwritingCase> UnderwritingCases => Set<UnderwritingCase>();

    public DbSet<PolicyLifecycle> PolicyLifecycles => Set<PolicyLifecycle>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Policy> Policies => Set<Policy>();

    public DbSet<PolicyPostIssuanceEvent> PolicyPostIssuanceEvents => Set<PolicyPostIssuanceEvent>();

    public DbSet<Endorsement> Endorsements => Set<Endorsement>();

    public DbSet<Claim> Claims => Set<Claim>();

    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();

    public DbSet<DocumentTemplateVersion> DocumentTemplateVersions => Set<DocumentTemplateVersion>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.Name).HasMaxLength(100).IsRequired();
            entity.Property(customer => customer.NationalId).HasMaxLength(20).IsRequired();
            entity.Property(customer => customer.Phone).HasMaxLength(30).IsRequired();
            entity.Property(customer => customer.Email).HasMaxLength(254);
            entity.Property(customer => customer.Address).HasMaxLength(300);
            entity.Property(customer => customer.ClaimRecordLevel).HasDefaultValue(4);
            entity.HasIndex(customer => customer.NationalId).IsUnique();
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(vehicle => vehicle.Id);
            entity.Property(vehicle => vehicle.LicensePlate).HasMaxLength(20).IsRequired();
            entity.Property(vehicle => vehicle.Vin).HasMaxLength(50).IsRequired();
            entity.Property(vehicle => vehicle.MakeModelCode).HasMaxLength(100).IsRequired();
            entity.Property(vehicle => vehicle.UsageType).HasMaxLength(50).IsRequired();
            entity.HasIndex(vehicle => vehicle.LicensePlate).IsUnique();
            entity.HasIndex(vehicle => vehicle.Vin).IsUnique();

            entity.HasOne(vehicle => vehicle.Customer)
                .WithMany(customer => customer.Vehicles)
                .HasForeignKey(vehicle => vehicle.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InsuranceType>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<InsuranceProduct>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasOne(x => x.InsuranceType)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.InsuranceTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RateVersion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Confidence).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.InsuranceProductId, x.Version }).IsUnique();
            entity.HasOne(x => x.InsuranceProduct)
                .WithMany(x => x.RateVersions)
                .HasForeignKey(x => x.InsuranceProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RateTable>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceVersion).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DisplayWarning).HasMaxLength(500).IsRequired();
            entity.Property(x => x.JsonData).IsRequired();
            entity.HasIndex(x => x.RateVersionId).IsUnique();
            entity.HasOne(x => x.RateVersion)
                .WithOne(x => x.RateTable)
                .HasForeignKey<RateTable>(x => x.RateVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuoteNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SalesAgent).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PremiumRequestJson).IsRequired();
            entity.Property(x => x.PremiumBreakdownJson).IsRequired();
            entity.Property(x => x.DisplayWarning).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CompulsoryPremium).HasPrecision(18, 2);
            entity.Property(x => x.VoluntaryPremium).HasPrecision(18, 2);
            entity.Property(x => x.TotalPremium).HasPrecision(18, 2);
            entity.HasIndex(x => x.QuoteNumber).IsUnique();
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.VehicleId);
            entity.HasIndex(x => x.RateVersionId);
            entity.HasIndex(x => x.RenewalSourcePolicyId)
                .IsUnique()
                .HasFilter("\"RenewalSourcePolicyId\" IS NOT NULL");
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Vehicle)
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RateVersion)
                .WithMany()
                .HasForeignKey(x => x.RateVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Policy>()
                .WithMany()
                .HasForeignKey(x => x.RenewalSourcePolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InsuranceApplication>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApplicationNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ApplicantName).HasMaxLength(100);
            entity.Property(x => x.ApplicantRelationshipToOwner).HasMaxLength(50);
            entity.Property(x => x.PaymentFrequency).HasMaxLength(20);
            entity.Property(x => x.InsuredName).HasMaxLength(100);
            entity.Property(x => x.InsuredNationalId).HasMaxLength(20);
            entity.Property(x => x.InsuredRelationshipToOwner).HasMaxLength(50);
            entity.Property(x => x.InsuredPhone).HasMaxLength(30);
            entity.Property(x => x.InsuredEmail).HasMaxLength(254);
            entity.Property(x => x.InsuredAddress).HasMaxLength(300);
            entity.Property(x => x.BeneficiaryDesignation).HasMaxLength(20);
            entity.HasIndex(x => x.ApplicationNumber).IsUnique();
            entity.HasIndex(x => x.QuoteId).IsUnique();
            entity.HasOne(x => x.Quote)
                .WithMany()
                .HasForeignKey(x => x.QuoteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationBeneficiary>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.NationalId).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RelationshipToInsured).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Percentage).HasPrecision(5, 2);
            entity.HasIndex(x => x.ApplicationId);
            entity.HasOne(x => x.Application)
                .WithMany(x => x.Beneficiaries)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationSignature>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SignerRole).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SignerName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SignatureDataUrl).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasIndex(x => x.ApplicationId);
            entity.HasIndex(x => new { x.ApplicationId, x.SignerRole }).IsUnique();
            entity.HasOne(x => x.Application)
                .WithMany(x => x.Signatures)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationDocument>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.StoredFileName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ReturnReason).HasMaxLength(500);
            entity.HasIndex(x => x.ApplicationId);
            entity.HasIndex(x => new { x.ApplicationId, x.DocumentType }).IsUnique();
            entity.HasOne(x => x.Application)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UnderwritingRule>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ConditionType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ConditionJson).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<UnderwritingResult>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RuleName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => x.ApplicationId);
            entity.HasIndex(x => new { x.ApplicationId, x.RuleId }).IsUnique();
            entity.HasOne(x => x.Application)
                .WithMany()
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Rule)
                .WithMany(x => x.Results)
                .HasForeignKey(x => x.RuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UnderwritingCase>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.OverallAction).HasMaxLength(30).IsRequired();
            entity.Property(x => x.RiskLevel).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Opinion).HasMaxLength(1000);
            entity.Property(x => x.Decision).HasMaxLength(30);
            entity.HasIndex(x => x.ApplicationId).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.Application)
                .WithOne()
                .HasForeignKey<UnderwritingCase>(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PolicyLifecycle>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.LastTransition).HasMaxLength(80);
            entity.Property(x => x.LastTransitionReason).HasMaxLength(500);
            entity.HasIndex(x => x.QuoteId).IsUnique();
            entity.HasIndex(x => x.ApplicationId).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.Quote)
                .WithOne()
                .HasForeignKey<PolicyLifecycle>(x => x.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Application)
                .WithOne()
                .HasForeignKey<PolicyLifecycle>(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PaymentNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PaymentFrequency).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ReferenceNumber).HasMaxLength(100);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => x.PaymentNumber).IsUnique();
            entity.HasIndex(x => x.ApplicationId).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.DueAtUtc);
            entity.HasOne(x => x.Application)
                .WithOne()
                .HasForeignKey<Payment>(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Policy>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PolicyNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.PaymentFrequency).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CustomerNationalId).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CustomerPhone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CustomerEmail).HasMaxLength(254);
            entity.Property(x => x.CustomerAddress).HasMaxLength(300);
            entity.Property(x => x.LicensePlate).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Vin).HasMaxLength(50).IsRequired();
            entity.Property(x => x.MakeModelCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UsageType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.InsuredName).HasMaxLength(100);
            entity.Property(x => x.InsuredNationalId).HasMaxLength(20);
            entity.Property(x => x.SalesAgent).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RateVersion).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TemplateVersion).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PdfStoredFileName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PdfContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PdfSha256).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.PolicyNumber).IsUnique();
            entity.HasIndex(x => x.ApplicationId).IsUnique();
            entity.HasIndex(x => x.PaymentId).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.InsuranceStartDate);
            entity.HasIndex(x => x.InsuranceEndDate);
            entity.HasOne(x => x.Application)
                .WithOne()
                .HasForeignKey<Policy>(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Payment)
                .WithOne()
                .HasForeignKey<Policy>(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Quote)
                .WithMany()
                .HasForeignKey(x => x.QuoteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PolicyPostIssuanceEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Step).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.PolicyId, x.Step }).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasOne(x => x.Policy)
                .WithMany()
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Endorsement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EndorsementNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EndorsementType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ChangeSummary).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ReviewOpinion).HasMaxLength(1000);
            entity.Property(x => x.AdjustmentCalculationRequestJson);
            entity.HasIndex(x => x.EndorsementNumber).IsUnique();
            entity.HasIndex(x => x.PolicyId);
            entity.HasIndex(x => x.PolicyId)
                .IsUnique()
                .HasFilter("Status IN ('draft', 'submitted', 'reviewing')");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.RequestedEffectiveDate);
            entity.HasOne(x => x.Policy)
                .WithMany()
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentTemplate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasMany(x => x.Versions)
                .WithOne(x => x.DocumentTemplate)
                .HasForeignKey(x => x.DocumentTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ActiveVersion)
                .WithMany()
                .HasForeignKey(x => x.ActiveVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentTemplateVersion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(10000);
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.DocumentTemplateId);
            entity.HasIndex(x => new { x.DocumentTemplateId, x.Version }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Recipient).HasMaxLength(254).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.Property(x => x.ReferenceType).HasMaxLength(60);
            entity.Property(x => x.ReferenceId).HasMaxLength(100);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Channel);
            entity.HasIndex(x => x.EventType);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.PasswordResetTokenHash).HasMaxLength(64);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.Role);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Actor).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ObjectType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ObjectId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SourceIp).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.Actor);
            entity.HasIndex(x => x.Role);
            entity.HasIndex(x => x.Action);
            entity.HasIndex(x => new { x.ObjectType, x.ObjectId });
        });

        modelBuilder.Entity<Claim>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClaimNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AccidentLocation).HasMaxLength(300).IsRequired();
            entity.Property(x => x.AccidentDescription).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ClaimedAmount).HasPrecision(18, 2);
            entity.Property(x => x.LiabilityDecision).HasMaxLength(500);
            entity.Property(x => x.ApprovedAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasMaxLength(100);
            entity.Property(x => x.DeniedReason).HasMaxLength(1000);
            entity.Property(x => x.AssignedAdjuster).HasMaxLength(100);
            entity.Property(x => x.LastTransition).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastTransitionReason).HasMaxLength(1000);
            entity.HasIndex(x => x.ClaimNumber).IsUnique();
            entity.HasIndex(x => x.PolicyId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.AccidentDate);
            entity.HasOne(x => x.Policy)
                .WithMany()
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
