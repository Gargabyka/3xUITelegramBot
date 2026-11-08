using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EFCore.Migrations
{
    /// <inheritdoc />
    public partial class HostingReminderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "HostingReminderStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastPaymentDate",
                table: "HostingReminderStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MessageChatId",
                table: "HostingReminderStates",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessageId",
                table: "HostingReminderStates",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageText",
                table: "HostingReminderStates",
                type: "TEXT",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodMonths",
                table: "HostingReminderStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "HostingReminderStates");

            migrationBuilder.DropColumn(
                name: "LastPaymentDate",
                table: "HostingReminderStates");

            migrationBuilder.DropColumn(
                name: "MessageChatId",
                table: "HostingReminderStates");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "HostingReminderStates");

            migrationBuilder.DropColumn(
                name: "MessageText",
                table: "HostingReminderStates");

            migrationBuilder.DropColumn(
                name: "PeriodMonths",
                table: "HostingReminderStates");
        }
    }
}
