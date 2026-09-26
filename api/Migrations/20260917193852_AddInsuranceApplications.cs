using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    QuoteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CurrentStep = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ApplicantRelationshipToOwner = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PaymentFrequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    InsuredName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    InsuredNationalId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    InsuredBirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InsuredRelationshipToOwner = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    InsuredPhone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    InsuredEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    InsuredAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    BeneficiaryDesignation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ApplicantSigned = table.Column<bool>(type: "INTEGER", nullable: false),
                    InsuredSigned = table.Column<bool>(type: "INTEGER", nullable: false),
                    AgentSigned = table.Column<bool>(type: "INTEGER", nullable: false),
                    TermsAccepted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ElectronicPolicyAccepted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationBeneficiaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NationalId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RelationshipToInsured = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationBeneficiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationBeneficiaries_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationBeneficiaries_ApplicationId",
                table: "ApplicationBeneficiaries",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApplicationNumber",
                table: "Applications",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_QuoteId",
                table: "Applications",
                column: "QuoteId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationBeneficiaries");

            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
