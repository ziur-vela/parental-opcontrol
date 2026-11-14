using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job22bDevicePower : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DevicePowerCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NoticeSeconds = table.Column<int>(type: "integer", nullable: false),
                    RequestedByParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultDetail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledByParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePowerCommands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePowerCommands_DeviceId_CreatedUtc",
                table: "DevicePowerCommands",
                columns: new[] { "DeviceId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePowerCommands_FamilyId_ChildId_CreatedUtc",
                table: "DevicePowerCommands",
                columns: new[] { "FamilyId", "ChildId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePowerCommands_Status_RunAtUtc",
                table: "DevicePowerCommands",
                columns: new[] { "Status", "RunAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevicePowerCommands");
        }
    }
}
