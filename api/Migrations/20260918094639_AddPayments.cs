using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PaymentNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PaymentFrequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AmountDue = table.Column<decimal>(type: "TEXT", nullable: false),
                    InstallmentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ApplicationId",
                table: "Payments",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DueAtUtc",
                table: "Payments",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentNumber",
                table: "Payments",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "Payments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");
        }
    }
}
