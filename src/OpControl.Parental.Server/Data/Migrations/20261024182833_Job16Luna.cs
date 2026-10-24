using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpControl.Parental.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Job16Luna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Luna",
                table: "PushPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChildLunaSettings",
                columns: table => new
                {
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    EnabledByParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnabledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnderThirteenAcknowledgedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DailyMessageLimit = table.Column<int>(type: "integer", nullable: false),
                    ExtraInstructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Theme = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildLunaSettings", x => x.ChildId);
                });

            migrationBuilder.CreateTable(
                name: "LunaChats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TitleSetByChild = table.Column<bool>(type: "boolean", nullable: false),
                    HiddenByChild = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SummaryUpToMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunaChats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LunaFamilySettings",
                columns: table => new
                {
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Setup = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ChatProvider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ChatModel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ChatKeyProtected = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ModerationKeyProtected = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    LastTestUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestOk = table.Column<bool>(type: "boolean", nullable: true),
                    LastTestError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MonthlyTokenCap = table.Column<int>(type: "integer", nullable: true),
                    HelplineText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunaFamilySettings", x => x.FamilyId);
                });

            migrationBuilder.CreateTable(
                name: "LunaLearnerProfiles",
                columns: table => new
                {
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunaLearnerProfiles", x => x.ChildId);
                });

            migrationBuilder.CreateTable(
                name: "LunaMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ShownText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Flag = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FlagCategories = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FlagScores = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarkedFine = table.Column<bool>(type: "boolean", nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunaMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LunaMoreRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GrantedAmount = table.Column<int>(type: "integer", nullable: true),
                    HandledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    HandledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunaMoreRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LunaTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunaTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LunaUsageDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Messages = table.Column<int>(type: "integer", nullable: false),
                    ExtraAllowed = table.Column<int>(type: "integer", nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    CooldownUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunaUsageDays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildLunaSettings_Enabled",
                table: "ChildLunaSettings",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_ChildLunaSettings_FamilyId",
                table: "ChildLunaSettings",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_LunaChats_ChildId_HiddenByChild_LastMessageUtc",
                table: "LunaChats",
                columns: new[] { "ChildId", "HiddenByChild", "LastMessageUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaChats_DeviceId_CreatedUtc",
                table: "LunaChats",
                columns: new[] { "DeviceId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaChats_FamilyId_ChildId_LastMessageUtc",
                table: "LunaChats",
                columns: new[] { "FamilyId", "ChildId", "LastMessageUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaFamilySettings_LastTestOk",
                table: "LunaFamilySettings",
                column: "LastTestOk");

            migrationBuilder.CreateIndex(
                name: "IX_LunaLearnerProfiles_FamilyId",
                table: "LunaLearnerProfiles",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_LunaMessages_ChatId_CreatedUtc",
                table: "LunaMessages",
                columns: new[] { "ChatId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaMessages_ChildId_Flag_CreatedUtc",
                table: "LunaMessages",
                columns: new[] { "ChildId", "Flag", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaMessages_FamilyId_ChildId_CreatedUtc",
                table: "LunaMessages",
                columns: new[] { "FamilyId", "ChildId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaMoreRequests_ChildId_Status_CreatedUtc",
                table: "LunaMoreRequests",
                columns: new[] { "ChildId", "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaMoreRequests_FamilyId_CreatedUtc",
                table: "LunaMoreRequests",
                columns: new[] { "FamilyId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaTokens_DeviceId_ChildId",
                table: "LunaTokens",
                columns: new[] { "DeviceId", "ChildId" });

            migrationBuilder.CreateIndex(
                name: "IX_LunaTokens_ExpiresUtc",
                table: "LunaTokens",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LunaTokens_TokenHash",
                table: "LunaTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LunaUsageDays_ChildId_Date",
                table: "LunaUsageDays",
                columns: new[] { "ChildId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LunaUsageDays_FamilyId_Date",
                table: "LunaUsageDays",
                columns: new[] { "FamilyId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChildLunaSettings");

            migrationBuilder.DropTable(
                name: "LunaChats");

            migrationBuilder.DropTable(
                name: "LunaFamilySettings");

            migrationBuilder.DropTable(
                name: "LunaLearnerProfiles");

            migrationBuilder.DropTable(
                name: "LunaMessages");

            migrationBuilder.DropTable(
                name: "LunaMoreRequests");

            migrationBuilder.DropTable(
                name: "LunaTokens");

            migrationBuilder.DropTable(
                name: "LunaUsageDays");

            migrationBuilder.DropColumn(
                name: "Luna",
                table: "PushPreferences");
        }
    }
}
