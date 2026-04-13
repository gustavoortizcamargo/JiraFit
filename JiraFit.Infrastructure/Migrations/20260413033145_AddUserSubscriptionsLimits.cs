using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraFit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSubscriptionsLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsPro",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageTrackedDate",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessagesSentToday",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsPro",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastMessageTrackedDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MessagesSentToday",
                table: "Users");
        }
    }
}
