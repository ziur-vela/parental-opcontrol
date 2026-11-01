using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job20eBrowserRulesOnline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnline",
                table: "Devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BrowserPageRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedByParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserPageRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrowserPageRules_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChildBrowserSettings",
                columns: table => new
                {
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockYouTubeShorts = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpdatedByParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildBrowserSettings", x => x.ChildId);
                    table.ForeignKey(
                        name: "FK_ChildBrowserSettings_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserPageRules_ChildId_Host_Path",
                table: "BrowserPageRules",
                columns: new[] { "ChildId", "Host", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrowserPageRules_FamilyId_ChildId",
                table: "BrowserPageRules",
                columns: new[] { "FamilyId", "ChildId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChildBrowserSettings_FamilyId",
                table: "ChildBrowserSettings",
                column: "FamilyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrowserPageRules");

            migrationBuilder.DropTable(
                name: "ChildBrowserSettings");

            migrationBuilder.DropColumn(
                name: "NotifyOnline",
                table: "Devices");
        }
    }
}
