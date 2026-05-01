#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class RenameFolderBackendColumnsToDriver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Folders_BackendType", table: "Folders");

            migrationBuilder.RenameColumn(
                name: "BackendConfig",
                table: "Folders",
                newName: "DriverConfig"
            );

            migrationBuilder.RenameColumn(
                name: "BackendType",
                table: "Folders",
                newName: "DriverType"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DriverType",
                table: "Folders",
                column: "DriverType"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Folders_DriverType", table: "Folders");

            migrationBuilder.RenameColumn(
                name: "DriverConfig",
                table: "Folders",
                newName: "BackendConfig"
            );

            migrationBuilder.RenameColumn(
                name: "DriverType",
                table: "Folders",
                newName: "BackendType"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_BackendType",
                table: "Folders",
                column: "BackendType"
            );
        }
    }
}
