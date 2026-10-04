using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CampaignBriefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Genre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tone = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CampaignLengthSessions = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionLengthMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Inclusions = table.Column<string>(type: "TEXT", nullable: false),
                    Exclusions = table.Column<string>(type: "TEXT", nullable: false),
                    StoryIdea = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SafetyBoundaries = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignBriefs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignBriefs");
        }
    }
}
