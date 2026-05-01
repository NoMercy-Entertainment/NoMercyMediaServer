#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LogoPath",
                table: "WatchProviders",
                newName: "Logo"
            );

            migrationBuilder.RenameColumn(name: "LogoPath", table: "Companies", newName: "Logo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Logo",
                table: "WatchProviders",
                newName: "LogoPath"
            );

            migrationBuilder.RenameColumn(name: "Logo", table: "Companies", newName: "LogoPath");
        }
    }
}
