using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cs2Admin.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomCfgToPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomCfg",
                table: "ServerPresets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CustomCfgName",
                table: "ServerPresets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomCfg",
                table: "ServerPresets");

            migrationBuilder.DropColumn(
                name: "CustomCfgName",
                table: "ServerPresets");
        }
    }
}
