using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cs2Admin.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Team1Name",
                table: "Lobbies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Team2Name",
                table: "Lobbies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Team1Name",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "Team2Name",
                table: "Lobbies");
        }
    }
}
