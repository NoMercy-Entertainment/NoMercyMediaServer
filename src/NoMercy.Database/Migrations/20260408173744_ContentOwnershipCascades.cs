using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class ContentOwnershipCascades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Libraries_LibraryId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_Artists_Libraries_LibraryId",
                table: "Artists");

            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Libraries_LibraryId",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreMovie_Movies_MovieId",
                table: "GenreMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Libraries_LibraryId",
                table: "Movies");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseGroups_Libraries_LibraryId",
                table: "ReleaseGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_Tvs_Libraries_LibraryId",
                table: "Tvs");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Movies_MovieId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_VideoFiles_VideoFileId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Movies_MovieId",
                table: "VideoFiles");

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Libraries_LibraryId",
                table: "Albums",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Artists_Libraries_LibraryId",
                table: "Artists",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Libraries_LibraryId",
                table: "Collections",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreMovie_Movies_MovieId",
                table: "GenreMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Libraries_LibraryId",
                table: "Movies",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseGroups_Libraries_LibraryId",
                table: "ReleaseGroups",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tvs_Libraries_LibraryId",
                table: "Tvs",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Movies_MovieId",
                table: "UserData",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_VideoFiles_VideoFileId",
                table: "UserData",
                column: "VideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Movies_MovieId",
                table: "VideoFiles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Libraries_LibraryId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_Artists_Libraries_LibraryId",
                table: "Artists");

            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Libraries_LibraryId",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreMovie_Movies_MovieId",
                table: "GenreMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Libraries_LibraryId",
                table: "Movies");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseGroups_Libraries_LibraryId",
                table: "ReleaseGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_Tvs_Libraries_LibraryId",
                table: "Tvs");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Movies_MovieId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_VideoFiles_VideoFileId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Movies_MovieId",
                table: "VideoFiles");

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Libraries_LibraryId",
                table: "Albums",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Artists_Libraries_LibraryId",
                table: "Artists",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Libraries_LibraryId",
                table: "Collections",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreMovie_Movies_MovieId",
                table: "GenreMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Libraries_LibraryId",
                table: "Movies",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseGroups_Libraries_LibraryId",
                table: "ReleaseGroups",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tvs_Libraries_LibraryId",
                table: "Tvs",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Movies_MovieId",
                table: "UserData",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_VideoFiles_VideoFileId",
                table: "UserData",
                column: "VideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Movies_MovieId",
                table: "VideoFiles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
