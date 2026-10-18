using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EFCore.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostingReminderStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemindersSentCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastSentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostingReminderStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramId = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    ReminderOptOut = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MaxConnections = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VpnPeers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PublicKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VpnPeers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VpnTrafficSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransferRx = table.Column<long>(type: "INTEGER", nullable: false),
                    TransferTx = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VpnTrafficSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VpnUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VpnUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VpnUsers_TelegramUsers_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "TelegramUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VpnUsers_VpnPeers_PeerId",
                        column: x => x.PeerId,
                        principalTable: "VpnPeers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramUsers_TelegramId",
                table: "TelegramUsers",
                column: "TelegramId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VpnPeers_ClientId",
                table: "VpnPeers",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VpnTrafficSnapshots_ClientId_CapturedAtUtc",
                table: "VpnTrafficSnapshots",
                columns: new[] { "ClientId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VpnUsers_PeerId",
                table: "VpnUsers",
                column: "PeerId");

            migrationBuilder.CreateIndex(
                name: "IX_VpnUsers_TelegramUserId_PeerId",
                table: "VpnUsers",
                columns: new[] { "TelegramUserId", "PeerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostingReminderStates");

            migrationBuilder.DropTable(
                name: "VpnTrafficSnapshots");

            migrationBuilder.DropTable(
                name: "VpnUsers");

            migrationBuilder.DropTable(
                name: "TelegramUsers");

            migrationBuilder.DropTable(
                name: "VpnPeers");
        }
    }
}
