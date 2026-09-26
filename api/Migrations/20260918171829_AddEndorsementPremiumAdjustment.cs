using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEndorsementPremiumAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AdjustmentAmountMinorUnits",
                table: "Endorsements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdjustmentCalculatedAtUtc",
                table: "Endorsements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentCalculationRequestJson",
                table: "Endorsements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverageDays",
                table: "Endorsements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OriginalPremiumMinorUnits",
                table: "Endorsements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecalculatedPremiumMinorUnits",
                table: "Endorsements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingDays",
                table: "Endorsements",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustmentAmountMinorUnits",
                table: "Endorsements");

            migrationBuilder.DropColumn(
                name: "AdjustmentCalculatedAtUtc",
                table: "Endorsements");

            migrationBuilder.DropColumn(
                name: "AdjustmentCalculationRequestJson",
                table: "Endorsements");

            migrationBuilder.DropColumn(
                name: "CoverageDays",
                table: "Endorsements");

            migrationBuilder.DropColumn(
                name: "OriginalPremiumMinorUnits",
                table: "Endorsements");

            migrationBuilder.DropColumn(
                name: "RecalculatedPremiumMinorUnits",
                table: "Endorsements");

            migrationBuilder.DropColumn(
                name: "RemainingDays",
                table: "Endorsements");
        }
    }
}
