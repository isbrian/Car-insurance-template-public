using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ActiveVersionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTemplateVersions_DocumentTemplates_DocumentTemplateId",
                        column: x => x.DocumentTemplateId,
                        principalTable: "DocumentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_ActiveVersionId",
                table: "DocumentTemplates",
                column: "ActiveVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_Code",
                table: "DocumentTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplateVersions_DocumentTemplateId",
                table: "DocumentTemplateVersions",
                column: "DocumentTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplateVersions_DocumentTemplateId_Version",
                table: "DocumentTemplateVersions",
                columns: new[] { "DocumentTemplateId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTemplates_DocumentTemplateVersions_ActiveVersionId",
                table: "DocumentTemplates",
                column: "ActiveVersionId",
                principalTable: "DocumentTemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentTemplates_DocumentTemplateVersions_ActiveVersionId",
                table: "DocumentTemplates");

            migrationBuilder.DropTable(
                name: "DocumentTemplateVersions");

            migrationBuilder.DropTable(
                name: "DocumentTemplates");
        }
    }
}
