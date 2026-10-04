using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignBibles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorldDescription = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CurrentSituation = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignBibles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignBibles_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tone = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    LowFright = table.Column<bool>(type: "INTEGER", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignSettings_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.UniqueConstraint("AK_Locations_CampaignId_Id", x => new { x.CampaignId, x.Id });
                    table.ForeignKey(
                        name: "FK_Locations_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                    table.UniqueConstraint("AK_Parties_CampaignId_Id", x => new { x.CampaignId, x.Id });
                    table.ForeignKey(
                        name: "FK_Parties_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quests_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectType = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetType = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Relationships_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.UniqueConstraint("AK_Sessions_CampaignId_Id", x => new { x.CampaignId, x.Id });
                    table.ForeignKey(
                        name: "FK_Sessions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Npcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Disposition = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Npcs_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Npcs_Locations_CampaignId_LocationId",
                        columns: x => new { x.CampaignId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "CampaignId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Heroes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Hearts = table.Column<int>(type: "INTEGER", nullable: false),
                    SparkleTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Heroes", x => x.Id);
                    table.UniqueConstraint("AK_Heroes_CampaignId_Id", x => new { x.CampaignId, x.Id });
                    table.ForeignKey(
                        name: "FK_Heroes_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Heroes_Parties_CampaignId_PartyId",
                        columns: x => new { x.CampaignId, x.PartyId },
                        principalTable: "Parties",
                        principalColumns: new[] { "CampaignId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignFacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Statement = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Importance = table.Column<int>(type: "INTEGER", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignFacts", x => x.Id);
                    table.CheckConstraint("CK_CampaignFacts_Importance", "Importance BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_CampaignFacts_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignFacts_Sessions_CampaignId_SourceSessionId",
                        columns: x => new { x.CampaignId, x.SourceSessionId },
                        principalTable: "Sessions",
                        principalColumns: new[] { "CampaignId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HeroId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Heroes_CampaignId_HeroId",
                        columns: x => new { x.CampaignId, x.HeroId },
                        principalTable: "Heroes",
                        principalColumns: new[] { "CampaignId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HeroId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsClaimed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rewards_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rewards_Heroes_CampaignId_HeroId",
                        columns: x => new { x.CampaignId, x.HeroId },
                        principalTable: "Heroes",
                        principalColumns: new[] { "CampaignId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignBibles_CampaignId",
                table: "CampaignBibles",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignFacts_CampaignId_SourceSessionId",
                table: "CampaignFacts",
                columns: new[] { "CampaignId", "SourceSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSettings_CampaignId",
                table: "CampaignSettings",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_CampaignId_PartyId",
                table: "Heroes",
                columns: new[] { "CampaignId", "PartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CampaignId_HeroId",
                table: "InventoryItems",
                columns: new[] { "CampaignId", "HeroId" });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_CampaignId_LocationId",
                table: "Npcs",
                columns: new[] { "CampaignId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_CampaignId",
                table: "Parties",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quests_CampaignId",
                table: "Quests",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Relationships_CampaignId",
                table: "Relationships",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_CampaignId_HeroId",
                table: "Rewards",
                columns: new[] { "CampaignId", "HeroId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CampaignId_SessionNumber",
                table: "Sessions",
                columns: new[] { "CampaignId", "SessionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignBibles");

            migrationBuilder.DropTable(
                name: "CampaignFacts");

            migrationBuilder.DropTable(
                name: "CampaignSettings");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Npcs");

            migrationBuilder.DropTable(
                name: "Quests");

            migrationBuilder.DropTable(
                name: "Relationships");

            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Heroes");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropTable(
                name: "Campaigns");
        }
    }
}
