using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job21WebSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WebSearches",
                table: "LunaUsageDays",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WebQuery",
                table: "LunaMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WebSearchRequests",
                table: "LunaMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WebSources",
                table: "LunaMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WebSearchEnabled",
                table: "ChildLunaSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebSearches",
                table: "LunaUsageDays");

            migrationBuilder.DropColumn(
                name: "WebQuery",
                table: "LunaMessages");

            migrationBuilder.DropColumn(
                name: "WebSearchRequests",
                table: "LunaMessages");

            migrationBuilder.DropColumn(
                name: "WebSources",
                table: "LunaMessages");

            migrationBuilder.DropColumn(
                name: "WebSearchEnabled",
                table: "ChildLunaSettings");
        }
    }
}
