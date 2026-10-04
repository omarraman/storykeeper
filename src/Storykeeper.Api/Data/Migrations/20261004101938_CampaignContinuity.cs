using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CampaignContinuity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE CampaignFacts SET Status = CASE Status WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE Status END " +
                "WHERE Status IN (2, 3);");

            migrationBuilder.CreateTable(
                name: "CampaignContinuityRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordType = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PreviousContent = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    NewContent = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignContinuityRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignContinuityRevisions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignContinuityRevisions_CampaignId_ChangedAtUtc",
                table: "CampaignContinuityRevisions",
                columns: new[] { "CampaignId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignContinuityRevisions_CampaignId_RecordType_RecordId",
                table: "CampaignContinuityRevisions",
                columns: new[] { "CampaignId", "RecordType", "RecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignContinuityRevisions");

            migrationBuilder.Sql(
                "UPDATE CampaignFacts SET Status = CASE Status WHEN 2 THEN 1 WHEN 3 THEN 2 WHEN 4 THEN 3 ELSE Status END " +
                "WHERE Status IN (2, 3, 4);");
        }
    }
}
