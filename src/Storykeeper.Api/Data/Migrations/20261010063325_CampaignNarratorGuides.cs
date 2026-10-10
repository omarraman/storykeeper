using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CampaignNarratorGuides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAtUtc",
                table: "CampaignDrafts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarratorGuide",
                table: "CampaignDrafts",
                type: "TEXT",
                maxLength: 30000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarratorGuide",
                table: "CampaignBriefs",
                type: "TEXT",
                maxLength: 30000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CampaignNarratorGuides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActiveText = table.Column<string>(type: "TEXT", maxLength: 30000, nullable: true),
                    PendingText = table.Column<string>(type: "TEXT", maxLength: 30000, nullable: false),
                    HasPendingRevision = table.Column<bool>(type: "INTEGER", nullable: false),
                    ActiveRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    PendingRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveApprovedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PendingUpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignNarratorGuides", x => x.Id);
                    table.CheckConstraint("CK_CampaignNarratorGuides_ActiveRevision", "ActiveRevision >= 0");
                    table.CheckConstraint("CK_CampaignNarratorGuides_PendingRevision", "PendingRevision >= 0");
                    table.ForeignKey(
                        name: "FK_CampaignNarratorGuides_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignNarratorGuides_CampaignId",
                table: "CampaignNarratorGuides",
                column: "CampaignId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignNarratorGuides");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "CampaignDrafts");

            migrationBuilder.DropColumn(
                name: "NarratorGuide",
                table: "CampaignDrafts");

            migrationBuilder.DropColumn(
                name: "NarratorGuide",
                table: "CampaignBriefs");
        }
    }
}
