using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SignerRole = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SignerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SignatureDataUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SignedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationSignatures_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSignatures_ApplicationId",
                table: "ApplicationSignatures",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSignatures_ApplicationId_SignerRole",
                table: "ApplicationSignatures",
                columns: new[] { "ApplicationId", "SignerRole" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSignatures");
        }
    }
}
