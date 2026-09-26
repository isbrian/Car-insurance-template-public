using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyPostIssuanceEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PolicyPostIssuanceEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PolicyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Step = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyPostIssuanceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyPostIssuanceEvents_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyPostIssuanceEvents_CreatedAtUtc",
                table: "PolicyPostIssuanceEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyPostIssuanceEvents_PolicyId_Step",
                table: "PolicyPostIssuanceEvents",
                columns: new[] { "PolicyId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyPostIssuanceEvents_Status",
                table: "PolicyPostIssuanceEvents",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyPostIssuanceEvents");
        }
    }
}
