using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InsuranceTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsuranceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InsuranceProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InsuranceTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsuranceProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InsuranceProducts_InsuranceTypes_InsuranceTypeId",
                        column: x => x.InsuranceTypeId,
                        principalTable: "InsuranceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RateVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InsuranceProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Confidence = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateVersions_InsuranceProducts_InsuranceProductId",
                        column: x => x.InsuranceProductId,
                        principalTable: "InsuranceProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceProducts_Code",
                table: "InsuranceProducts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceProducts_InsuranceTypeId",
                table: "InsuranceProducts",
                column: "InsuranceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceTypes_Code",
                table: "InsuranceTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RateVersions_InsuranceProductId_Version",
                table: "RateVersions",
                columns: new[] { "InsuranceProductId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RateVersions");

            migrationBuilder.DropTable(
                name: "InsuranceProducts");

            migrationBuilder.DropTable(
                name: "InsuranceTypes");
        }
    }
}
