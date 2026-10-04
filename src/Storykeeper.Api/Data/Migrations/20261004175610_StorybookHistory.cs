using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storykeeper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StorybookHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Sessions",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "Adventure");

            migrationBuilder.Sql("UPDATE Sessions SET Title = 'Adventure ' || SessionNumber WHERE Title = 'Adventure'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Sessions");
        }
    }
}
