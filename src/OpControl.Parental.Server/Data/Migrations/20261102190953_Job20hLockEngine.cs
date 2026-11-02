using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job20hLockEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardLimit",
                table: "UsageIntervals",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BonusStartUtc",
                table: "LockStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockStartUtc",
                table: "LockStates",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountsTowardLimit",
                table: "UsageIntervals");

            migrationBuilder.DropColumn(
                name: "BonusStartUtc",
                table: "LockStates");

            migrationBuilder.DropColumn(
                name: "UnlockStartUtc",
                table: "LockStates");
        }
    }
}
