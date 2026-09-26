using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RenewalSourcePolicyId",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_RenewalSourcePolicyId",
                table: "Quotes",
                column: "RenewalSourcePolicyId",
                unique: true,
                filter: "\"RenewalSourcePolicyId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Policies_RenewalSourcePolicyId",
                table: "Quotes",
                column: "RenewalSourcePolicyId",
                principalTable: "Policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Policies_RenewalSourcePolicyId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_RenewalSourcePolicyId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RenewalSourcePolicyId",
                table: "Quotes");
        }
    }
}
