using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActiveSessionStoryTurnHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActingHeroId",
                table: "StoryBeats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "StoryBeats",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckResolutionId",
                table: "StoryBeats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChronologyEstimated",
                table: "StoryBeats",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NpcDialogueJson",
                table: "StoryBeats",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "StoryBeats",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH RankedStoryBeats AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY CampaignId, SessionId
                               ORDER BY CreatedAtUtc, Id
                           ) AS SequenceNumber
                    FROM StoryBeats
                )
                UPDATE StoryBeats
                SET SequenceNumber = (
                    SELECT RankedStoryBeats.SequenceNumber
                    FROM RankedStoryBeats
                    WHERE RankedStoryBeats.Id = StoryBeats.Id
                ),
                ChronologyEstimated = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StoryBeats_CampaignId_SessionId_SequenceNumber",
                table: "StoryBeats",
                columns: new[] { "CampaignId", "SessionId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoryBeats_CampaignId_SessionId_SequenceNumber",
                table: "StoryBeats");

            migrationBuilder.DropColumn(
                name: "ActingHeroId",
                table: "StoryBeats");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "StoryBeats");

            migrationBuilder.DropColumn(
                name: "CheckResolutionId",
                table: "StoryBeats");

            migrationBuilder.DropColumn(
                name: "ChronologyEstimated",
                table: "StoryBeats");

            migrationBuilder.DropColumn(
                name: "NpcDialogueJson",
                table: "StoryBeats");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "StoryBeats");
        }
    }
}
