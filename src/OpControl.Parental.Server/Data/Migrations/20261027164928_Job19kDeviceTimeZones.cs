using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job19kDeviceTimeZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "FamilySettings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedTimeZoneId",
                table: "Devices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneOverrideId",
                table: "Devices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            // Keep the raw client id and store the IANA id the dashboard uses.
            migrationBuilder.Sql(@"
UPDATE ""Devices""
   SET ""ReportedTimeZoneId"" = ""TimeZoneId"",
       ""TimeZoneId"" = CASE ""TimeZoneId""
           WHEN 'US Mountain Standard Time' THEN 'America/Phoenix'
           WHEN 'Pacific Standard Time' THEN 'America/Los_Angeles'
           WHEN 'Mountain Standard Time' THEN 'America/Denver'
           WHEN 'Central Standard Time' THEN 'America/Chicago'
           WHEN 'Eastern Standard Time' THEN 'America/New_York'
           WHEN 'Mountain Standard Time (Mexico)' THEN 'America/Chihuahua'
           WHEN 'Central Standard Time (Mexico)' THEN 'America/Mexico_City'
           ELSE ""TimeZoneId""
       END
 WHERE ""ReportedTimeZoneId"" IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "FamilySettings");

            migrationBuilder.DropColumn(
                name: "ReportedTimeZoneId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "TimeZoneOverrideId",
                table: "Devices");
        }
    }
}
