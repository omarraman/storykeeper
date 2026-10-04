using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParentControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParentInstruction",
                table: "Sessions",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetySettings",
                table: "CampaignSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"fearLevel\":1,\"combatMode\":0,\"excludedContent\":[],\"maxNarrationWords\":120,\"sessionLengthMinutes\":45}");

            migrationBuilder.AddColumn<string>(
                name: "SafetySettings",
                table: "CampaignBriefs",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"fearLevel\":1,\"combatMode\":0,\"excludedContent\":[],\"maxNarrationWords\":120,\"sessionLengthMinutes\":45}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ParentInstruction",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SafetySettings",
                table: "CampaignSettings");

            migrationBuilder.DropColumn(
                name: "SafetySettings",
                table: "CampaignBriefs");
        }
    }
}
