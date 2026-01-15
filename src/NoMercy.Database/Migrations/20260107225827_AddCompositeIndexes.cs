using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_EpisodeId_Folder",
                table: "VideoFiles",
                columns: new[] { "EpisodeId", "Folder" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_MovieId_Folder",
                table: "VideoFiles",
                columns: new[] { "MovieId", "Folder" });

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId_LastPlayedDate",
                table: "UserData",
                columns: new[] { "UserId", "LastPlayedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Tvs_LibraryId_TitleSort",
                table: "Tvs",
                columns: new[] { "LibraryId", "TitleSort" });

            migrationBuilder.CreateIndex(
                name: "IX_Movies_LibraryId_TitleSort",
                table: "Movies",
                columns: new[] { "LibraryId", "TitleSort" });

            migrationBuilder.CreateIndex(
                name: "IX_Images_CollectionId_Type",
                table: "Images",
                columns: new[] { "CollectionId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Images_MovieId_Type",
                table: "Images",
                columns: new[] { "MovieId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Images_TvId_Type",
                table: "Images",
                columns: new[] { "TvId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Images_Type_Iso6391",
                table: "Images",
                columns: new[] { "Type", "Iso6391" });

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_TvId_SeasonNumber",
                table: "Episodes",
                columns: new[] { "TvId", "SeasonNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_TvId_SeasonNumber_EpisodeNumber",
                table: "Episodes",
                columns: new[] { "TvId", "SeasonNumber", "EpisodeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionMovie_MovieId_CollectionId",
                table: "CollectionMovie",
                columns: new[] { "MovieId", "CollectionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoFiles_EpisodeId_Folder",
                table: "VideoFiles");

            migrationBuilder.DropIndex(
                name: "IX_VideoFiles_MovieId_Folder",
                table: "VideoFiles");

            migrationBuilder.DropIndex(
                name: "IX_UserData_UserId_LastPlayedDate",
                table: "UserData");

            migrationBuilder.DropIndex(
                name: "IX_Tvs_LibraryId_TitleSort",
                table: "Tvs");

            migrationBuilder.DropIndex(
                name: "IX_Movies_LibraryId_TitleSort",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Images_CollectionId_Type",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_MovieId_Type",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_TvId_Type",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_Type_Iso6391",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_TvId_SeasonNumber",
                table: "Episodes");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_TvId_SeasonNumber_EpisodeNumber",
                table: "Episodes");

            migrationBuilder.DropIndex(
                name: "IX_CollectionMovie_MovieId_CollectionId",
                table: "CollectionMovie");
        }
    }
}
