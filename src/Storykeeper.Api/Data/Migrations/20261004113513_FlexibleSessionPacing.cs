using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlexibleSessionPacing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Quests_SessionLength",
                table: "Quests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdventureDrafts_SessionLength",
                table: "AdventureDrafts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Quests_SessionLength",
                table: "Quests",
                sql: "SessionLengthMinutes IS NULL OR SessionLengthMinutes BETWEEN 15 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdventureDrafts_SessionLength",
                table: "AdventureDrafts",
                sql: "SessionLengthMinutes BETWEEN 15 AND 180");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Quests_SessionLength",
                table: "Quests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdventureDrafts_SessionLength",
                table: "AdventureDrafts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Quests_SessionLength",
                table: "Quests",
                sql: "SessionLengthMinutes IS NULL OR SessionLengthMinutes IN (30, 45, 60)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdventureDrafts_SessionLength",
                table: "AdventureDrafts",
                sql: "SessionLengthMinutes IN (30, 45, 60)");
        }
    }
}
