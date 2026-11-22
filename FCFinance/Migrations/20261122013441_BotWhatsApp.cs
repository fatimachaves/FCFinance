using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCFinance.Migrations
{
    /// <inheritdoc />
    public partial class BotWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    PhoneNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Step = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValueTypeId = table.Column<int>(type: "int", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailedCodeAttempts = table.Column<int>(type: "int", nullable: false),
                    FailedCodeWindowStart = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.PhoneNumber);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedChatMessages",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedChatMessages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppLinkCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppLinkCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedChatMessages_ReceivedAt",
                table: "ProcessedChatMessages",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppLinkCodes_Code",
                table: "WhatsAppLinkCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppLinkCodes_UserId",
                table: "WhatsAppLinkCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppLinks_PhoneNumber",
                table: "WhatsAppLinks",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppLinks_UserId",
                table: "WhatsAppLinks",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatSessions");

            migrationBuilder.DropTable(
                name: "ProcessedChatMessages");

            migrationBuilder.DropTable(
                name: "WhatsAppLinkCodes");

            migrationBuilder.DropTable(
                name: "WhatsAppLinks");
        }
    }
}
