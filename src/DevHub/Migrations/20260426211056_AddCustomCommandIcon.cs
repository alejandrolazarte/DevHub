using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomCommandIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "CustomRepoCommands",
                type: "TEXT",
                nullable: false,
                defaultValue: "terminal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Icon",
                table: "CustomRepoCommands");
        }
    }
}
