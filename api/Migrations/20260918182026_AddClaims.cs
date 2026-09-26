using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClaimNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PolicyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AccidentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccidentLocation = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    AccidentDescription = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ClaimedAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    AssignedAdjuster = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastTransition = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    LastTransitionReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    StatusChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_AccidentDate",
                table: "Claims",
                column: "AccidentDate");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ClaimNumber",
                table: "Claims",
                column: "ClaimNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PolicyId",
                table: "Claims",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status",
                table: "Claims",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Claims");
        }
    }
}
