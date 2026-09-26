using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuoteNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    RateVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    InsuranceStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SalesAgent = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PremiumRequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    PremiumBreakdownJson = table.Column<string>(type: "TEXT", nullable: false),
                    CompulsoryPremium = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    VoluntaryPremium = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalPremium = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IsProvisional = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayWarning = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_RateVersions_RateVersionId",
                        column: x => x.RateVersionId,
                        principalTable: "RateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CustomerId",
                table: "Quotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_QuoteNumber",
                table: "Quotes",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_RateVersionId",
                table: "Quotes",
                column: "RateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_VehicleId",
                table: "Quotes",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Quotes");
        }
    }
}
