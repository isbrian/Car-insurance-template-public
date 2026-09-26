using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarInsurance.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ReferenceType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    ReferenceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Channel",
                table: "Notifications",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAtUtc",
                table: "Notifications",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EventType",
                table: "Notifications",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReferenceType_ReferenceId",
                table: "Notifications",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Status",
                table: "Notifications",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
