using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job19jWebsitesOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Host",
                table: "VisitRecords",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOffline",
                table: "Devices",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Existing computers default to no offline push/email; phones keep it on.
            migrationBuilder.Sql("UPDATE \"Devices\" SET \"NotifyOffline\" = FALSE WHERE \"Platform\" IN ('Windows', 'Linux');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Host",
                table: "VisitRecords");

            migrationBuilder.DropColumn(
                name: "NotifyOffline",
                table: "Devices");
        }
    }
}
