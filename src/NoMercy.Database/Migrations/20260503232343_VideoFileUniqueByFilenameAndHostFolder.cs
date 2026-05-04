using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class VideoFileUniqueByFilenameAndHostFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_VideoFiles_Filename", table: "VideoFiles");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_Filename_HostFolder",
                table: "VideoFiles",
                columns: new[] { "Filename", "HostFolder" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoFiles_Filename_HostFolder",
                table: "VideoFiles"
            );

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_Filename",
                table: "VideoFiles",
                column: "Filename",
                unique: true
            );
        }
    }
}
