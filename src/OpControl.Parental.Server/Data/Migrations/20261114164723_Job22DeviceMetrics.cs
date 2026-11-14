using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job22DeviceMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlertOnDeviceWarnings",
                table: "FamilySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MetricsBatteryPercent",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetricsBatteryState",
                table: "Devices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetricsCpuPercent",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MetricsDiskFreeBytes",
                table: "Devices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MetricsDiskTotalBytes",
                table: "Devices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MetricsRamTotalBytes",
                table: "Devices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MetricsRamUsedBytes",
                table: "Devices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MetricsUpdatedUtc",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MetricsUptimeSeconds",
                table: "Devices",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceWarnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeakValue = table.Column<double>(type: "double precision", nullable: false),
                    Threshold = table.Column<double>(type: "double precision", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceWarnings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceWarnings_DeviceId_Kind_EndedUtc",
                table: "DeviceWarnings",
                columns: new[] { "DeviceId", "Kind", "EndedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceWarnings_DeviceId_StartedUtc",
                table: "DeviceWarnings",
                columns: new[] { "DeviceId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceWarnings_FamilyId_ChildId_StartedUtc",
                table: "DeviceWarnings",
                columns: new[] { "FamilyId", "ChildId", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceWarnings");

            migrationBuilder.DropColumn(
                name: "AlertOnDeviceWarnings",
                table: "FamilySettings");

            migrationBuilder.DropColumn(
                name: "MetricsBatteryPercent",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MetricsBatteryState",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MetricsCpuPercent",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MetricsDiskFreeBytes",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MetricsDiskTotalBytes",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MetricsRamTotalBytes",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MetricsRamUsedBytes",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MetricsUpdatedUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MetricsUptimeSeconds",
                table: "Devices");
        }
    }
}
