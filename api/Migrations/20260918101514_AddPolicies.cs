using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PolicyNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuoteId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    InsuranceStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InsuranceEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalPremium = table.Column<decimal>(type: "TEXT", nullable: false),
                    PaymentFrequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CustomerNationalId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CustomerPhone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    CustomerAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    LicensePlate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Vin = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MakeModelCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UsageType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InsuredName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    InsuredNationalId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SalesAgent = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RateVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TemplateVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PdfStoredFileName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PdfContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PdfSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    PdfSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Policies_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Policies_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Policies_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_ApplicationId",
                table: "Policies",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_InsuranceEndDate",
                table: "Policies",
                column: "InsuranceEndDate");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_InsuranceStartDate",
                table: "Policies",
                column: "InsuranceStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_PaymentId",
                table: "Policies",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_PolicyNumber",
                table: "Policies",
                column: "PolicyNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_QuoteId",
                table: "Policies",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_Status",
                table: "Policies",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Policies");
        }
    }
}
