using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RateTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RateVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsProvisional = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayWarning = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    JsonData = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateTables_RateVersions_RateVersionId",
                        column: x => x.RateVersionId,
                        principalTable: "RateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RateTables_RateVersionId",
                table: "RateTables",
                column: "RateVersionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RateTables");
        }
    }
}
