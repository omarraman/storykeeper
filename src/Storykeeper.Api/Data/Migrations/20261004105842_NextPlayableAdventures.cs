using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NextPlayableAdventures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdventurePlanJson",
                table: "Quests",
                type: "TEXT",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionLengthMinutes",
                table: "Quests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdventureDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionLengthMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentPreferences = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GenerationNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false),
                    ActivatedQuestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdventureDrafts", x => x.Id);
                    table.CheckConstraint("CK_AdventureDrafts_GenerationNumber", "GenerationNumber >= 1");
                    table.CheckConstraint("CK_AdventureDrafts_SessionLength", "SessionLengthMinutes IN (30, 45, 60)");
                    table.ForeignKey(
                        name: "FK_AdventureDrafts_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Quests_SessionLength",
                table: "Quests",
                sql: "SessionLengthMinutes IS NULL OR SessionLengthMinutes IN (30, 45, 60)");

            migrationBuilder.CreateIndex(
                name: "IX_AdventureDrafts_ActivatedQuestId",
                table: "AdventureDrafts",
                column: "ActivatedQuestId",
                unique: true,
                filter: "ActivatedQuestId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AdventureDrafts_CampaignId_UpdatedAtUtc",
                table: "AdventureDrafts",
                columns: new[] { "CampaignId", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdventureDrafts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Quests_SessionLength",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "AdventurePlanJson",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "SessionLengthMinutes",
                table: "Quests");
        }
    }
}
