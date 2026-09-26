using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUnderwritingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnderwritingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ConditionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ConditionJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderwritingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnderwritingResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RuleName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderwritingResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnderwritingResults_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnderwritingResults_UnderwritingRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "UnderwritingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingResults_ApplicationId",
                table: "UnderwritingResults",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingResults_ApplicationId_RuleId",
                table: "UnderwritingResults",
                columns: new[] { "ApplicationId", "RuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingResults_RuleId",
                table: "UnderwritingResults",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingRules_Code",
                table: "UnderwritingRules",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnderwritingResults");

            migrationBuilder.DropTable(
                name: "UnderwritingRules");
        }
    }
}
