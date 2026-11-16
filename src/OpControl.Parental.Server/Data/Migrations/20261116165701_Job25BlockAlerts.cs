using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job25BlockAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Background",
                table: "VisitRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReduceMicrosoftBackgroundConnections",
                table: "Devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MutedBlockedSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedByParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MutedBlockedSites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MutedBlockedSites_FamilyId_ChildId",
                table: "MutedBlockedSites",
                columns: new[] { "FamilyId", "ChildId" });

            migrationBuilder.CreateIndex(
                name: "IX_MutedBlockedSites_FamilyId_ChildId_Domain",
                table: "MutedBlockedSites",
                columns: new[] { "FamilyId", "ChildId", "Domain" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MutedBlockedSites");

            migrationBuilder.DropColumn(
                name: "Background",
                table: "VisitRecords");

            migrationBuilder.DropColumn(
                name: "ReduceMicrosoftBackgroundConnections",
                table: "Devices");
        }
    }
}
