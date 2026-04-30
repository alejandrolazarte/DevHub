using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHub.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        private static readonly string[] HiddenAutoCommandsIndex = ["RepoPath", "Name"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Canvases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CytoscapeJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Canvases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomRepoCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepoPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Command = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomRepoCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Prefixes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HiddenAutoCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepoPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenAutoCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepoCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Path = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    AddedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepoCatalogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Canvases_Name",
                table: "Canvases",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HiddenAutoCommands_RepoPath_Name",
                table: "HiddenAutoCommands",
                columns: HiddenAutoCommandsIndex,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepoCatalogEntries_Path",
                table: "RepoCatalogEntries",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Canvases");

            migrationBuilder.DropTable(
                name: "CustomRepoCommands");

            migrationBuilder.DropTable(
                name: "GroupRules");

            migrationBuilder.DropTable(
                name: "HiddenAutoCommands");

            migrationBuilder.DropTable(
                name: "RepoCatalogEntries");
        }
    }
}
