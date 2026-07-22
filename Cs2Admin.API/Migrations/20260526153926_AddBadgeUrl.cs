using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cs2Admin.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgeUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BadgeUrl",
                table: "Maps",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BadgeUrl",
                table: "Maps");
        }
    }
}
