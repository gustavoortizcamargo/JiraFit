using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraFit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsVerificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "DashboardUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VerificationCode",
                table: "DashboardUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationExpiresAt",
                table: "DashboardUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "DashboardUsers");

            migrationBuilder.DropColumn(
                name: "VerificationCode",
                table: "DashboardUsers");

            migrationBuilder.DropColumn(
                name: "VerificationExpiresAt",
                table: "DashboardUsers");
        }
    }
}
