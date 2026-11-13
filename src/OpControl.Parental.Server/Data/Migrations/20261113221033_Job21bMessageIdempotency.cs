using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job21bMessageIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientMessageId",
                table: "ChildMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildMessages_DeviceId_ClientMessageId",
                table: "ChildMessages",
                columns: new[] { "DeviceId", "ClientMessageId" },
                unique: true,
                filter: "\"ClientMessageId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChildMessages_DeviceId_ClientMessageId",
                table: "ChildMessages");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "ChildMessages");
        }
    }
}
