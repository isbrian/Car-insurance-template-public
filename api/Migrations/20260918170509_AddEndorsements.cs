using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEndorsements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Endorsements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EndorsementNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PolicyId = table.Column<int>(type: "INTEGER", nullable: false),
                    EndorsementType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    RequestedEffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ReviewOpinion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewingAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Endorsements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Endorsements_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Endorsements_EndorsementNumber",
                table: "Endorsements",
                column: "EndorsementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Endorsements_PolicyId",
                table: "Endorsements",
                column: "PolicyId",
                unique: true,
                filter: "Status IN ('draft', 'submitted', 'reviewing')");

            migrationBuilder.CreateIndex(
                name: "IX_Endorsements_RequestedEffectiveDate",
                table: "Endorsements",
                column: "RequestedEffectiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_Endorsements_Status",
                table: "Endorsements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Endorsements");
        }
    }
}
