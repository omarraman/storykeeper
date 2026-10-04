using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CampaignDraftGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "CampaignBibles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "CampaignDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignBriefId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GenerationNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", maxLength: 30000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignDrafts_CampaignBriefs_CampaignBriefId",
                        column: x => x.CampaignBriefId,
                        principalTable: "CampaignBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignDrafts_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignBibleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceDraftId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", maxLength: 30000, nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignBibleVersions", x => x.Id);
                    table.CheckConstraint("CK_CampaignBibleVersions_Version", "Version >= 1");
                    table.ForeignKey(
                        name: "FK_CampaignBibleVersions_CampaignDrafts_SourceDraftId",
                        column: x => x.SourceDraftId,
                        principalTable: "CampaignDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CampaignBibleVersions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CampaignBibles_Version",
                table: "CampaignBibles",
                sql: "Version >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignBibleVersions_CampaignId_Version",
                table: "CampaignBibleVersions",
                columns: new[] { "CampaignId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignBibleVersions_SourceDraftId",
                table: "CampaignBibleVersions",
                column: "SourceDraftId",
                unique: true,
                filter: "SourceDraftId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignDrafts_CampaignBriefId",
                table: "CampaignDrafts",
                column: "CampaignBriefId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignDrafts_CampaignId",
                table: "CampaignDrafts",
                column: "CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignBibleVersions");

            migrationBuilder.DropTable(
                name: "CampaignDrafts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignBibles_Version",
                table: "CampaignBibles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CampaignBibles");
        }
    }
}
