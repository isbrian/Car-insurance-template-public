using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUnderwritingQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnderwritingCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    OverallAction = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Opinion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderwritingCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnderwritingCases_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingCases_ApplicationId",
                table: "UnderwritingCases",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingCases_Status",
                table: "UnderwritingCases",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnderwritingCases");
        }
    }
}
