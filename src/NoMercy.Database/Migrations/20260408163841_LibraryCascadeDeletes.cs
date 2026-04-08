using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class LibraryCascadeDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumLibrary_Libraries_LibraryId",
                table: "AlbumLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistLibrary_Libraries_LibraryId",
                table: "ArtistLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionLibrary_Libraries_LibraryId",
                table: "CollectionLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderLibrary_Libraries_LibraryId",
                table: "FolderLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_LanguageLibrary_Libraries_LibraryId",
                table: "LanguageLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryMovie_Libraries_LibraryId",
                table: "LibraryMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTrack_Libraries_LibraryId",
                table: "LibraryTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTv_Libraries_LibraryId",
                table: "LibraryTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryUser_Libraries_LibraryId",
                table: "LibraryUser");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Libraries_LibraryId",
                table: "PlaybackPreferences");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumLibrary_Libraries_LibraryId",
                table: "AlbumLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistLibrary_Libraries_LibraryId",
                table: "ArtistLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionLibrary_Libraries_LibraryId",
                table: "CollectionLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderLibrary_Libraries_LibraryId",
                table: "FolderLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LanguageLibrary_Libraries_LibraryId",
                table: "LanguageLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryMovie_Libraries_LibraryId",
                table: "LibraryMovie",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTrack_Libraries_LibraryId",
                table: "LibraryTrack",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTv_Libraries_LibraryId",
                table: "LibraryTv",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryUser_Libraries_LibraryId",
                table: "LibraryUser",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Libraries_LibraryId",
                table: "PlaybackPreferences",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumLibrary_Libraries_LibraryId",
                table: "AlbumLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistLibrary_Libraries_LibraryId",
                table: "ArtistLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionLibrary_Libraries_LibraryId",
                table: "CollectionLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderLibrary_Libraries_LibraryId",
                table: "FolderLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_LanguageLibrary_Libraries_LibraryId",
                table: "LanguageLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryMovie_Libraries_LibraryId",
                table: "LibraryMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTrack_Libraries_LibraryId",
                table: "LibraryTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTv_Libraries_LibraryId",
                table: "LibraryTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryUser_Libraries_LibraryId",
                table: "LibraryUser");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Libraries_LibraryId",
                table: "PlaybackPreferences");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumLibrary_Libraries_LibraryId",
                table: "AlbumLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistLibrary_Libraries_LibraryId",
                table: "ArtistLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionLibrary_Libraries_LibraryId",
                table: "CollectionLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderLibrary_Libraries_LibraryId",
                table: "FolderLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LanguageLibrary_Libraries_LibraryId",
                table: "LanguageLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryMovie_Libraries_LibraryId",
                table: "LibraryMovie",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTrack_Libraries_LibraryId",
                table: "LibraryTrack",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTv_Libraries_LibraryId",
                table: "LibraryTv",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryUser_Libraries_LibraryId",
                table: "LibraryUser",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Libraries_LibraryId",
                table: "PlaybackPreferences",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
