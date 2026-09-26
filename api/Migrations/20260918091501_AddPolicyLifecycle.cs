using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PolicyLifecycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuoteId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LastTransition = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    LastTransitionReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyLifecycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyLifecycles_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PolicyLifecycles_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyLifecycles_ApplicationId",
                table: "PolicyLifecycles",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyLifecycles_QuoteId",
                table: "PolicyLifecycles",
                column: "QuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyLifecycles_Status",
                table: "PolicyLifecycles",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyLifecycles");
        }
    }
}
