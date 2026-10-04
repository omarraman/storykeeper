using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChildFriendlyRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Strengths",
                table: "Heroes",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.CreateTable(
                name: "CheckResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HeroId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Roll = table.Column<int>(type: "INTEGER", nullable: false),
                    RollSource = table.Column<int>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    Target = table.Column<int>(type: "INTEGER", nullable: false),
                    Strength = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    StrengthBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    SparkleBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    ForwardProgressRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConsequenceCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    Risky = table.Column<bool>(type: "INTEGER", nullable: false),
                    SparkleTokenSpent = table.Column<bool>(type: "INTEGER", nullable: false),
                    HeartsBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    HeartsAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    SparkleTokensBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    SparkleTokensAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckResolutions", x => x.Id);
                    table.CheckConstraint("CK_CheckResolutions_Roll", "Roll BETWEEN 1 AND 20");
                    table.CheckConstraint("CK_CheckResolutions_Target", "Target IN (8, 12, 16)");
                    table.ForeignKey(
                        name: "FK_CheckResolutions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckResolutions_Heroes_CampaignId_HeroId",
                        columns: x => new { x.CampaignId, x.HeroId },
                        principalTable: "Heroes",
                        principalColumns: new[] { "CampaignId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckResolutions_Sessions_CampaignId_SessionId",
                        columns: x => new { x.CampaignId, x.SessionId },
                        principalTable: "Sessions",
                        principalColumns: new[] { "CampaignId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckResolutions_CampaignId_HeroId",
                table: "CheckResolutions",
                columns: new[] { "CampaignId", "HeroId" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckResolutions_CampaignId_SessionId",
                table: "CheckResolutions",
                columns: new[] { "CampaignId", "SessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckResolutions");

            migrationBuilder.DropColumn(
                name: "Strengths",
                table: "Heroes");
        }
    }
}
