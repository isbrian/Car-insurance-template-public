using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimSettlementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "Claims",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeniedReason",
                table: "Claims",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiabilityDecision",
                table: "Claims",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LiabilityPercentage",
                table: "Claims",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAtUtc",
                table: "Claims",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Claims",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "DeniedReason",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "LiabilityDecision",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "LiabilityPercentage",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Claims");
        }
    }
}
