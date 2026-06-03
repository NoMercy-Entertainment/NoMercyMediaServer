using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class ColorPalettePendingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TvShows_ColorPalette_pending",
                table: "Tvs",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Similar_ColorPalette_pending",
                table: "Similar",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_ColorPalette_pending",
                table: "Seasons",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseGroups_ColorPalette_pending",
                table: "ReleaseGroups",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_ColorPalette_pending",
                table: "Recommendations",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_ColorPalette_pending",
                table: "Playlists",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_People_ColorPalette_pending",
                table: "People",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Movies_ColorPalette_pending",
                table: "Movies",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_ColorPalette_pending",
                table: "Images",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_ColorPalette_pending",
                table: "Episodes",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Collections_ColorPalette_pending",
                table: "Collections",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Artists_ColorPalette_pending",
                table: "Artists",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Albums_ColorPalette_pending",
                table: "Albums",
                column: "ColorPalette",
                filter: "ColorPalette IS NULL OR ColorPalette = ''"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_TvShows_ColorPalette_pending", table: "Tvs");

            migrationBuilder.DropIndex(name: "IX_Similar_ColorPalette_pending", table: "Similar");

            migrationBuilder.DropIndex(name: "IX_Seasons_ColorPalette_pending", table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_ReleaseGroups_ColorPalette_pending",
                table: "ReleaseGroups"
            );

            migrationBuilder.DropIndex(
                name: "IX_Recommendations_ColorPalette_pending",
                table: "Recommendations"
            );

            migrationBuilder.DropIndex(
                name: "IX_Playlists_ColorPalette_pending",
                table: "Playlists"
            );

            migrationBuilder.DropIndex(name: "IX_People_ColorPalette_pending", table: "People");

            migrationBuilder.DropIndex(name: "IX_Movies_ColorPalette_pending", table: "Movies");

            migrationBuilder.DropIndex(name: "IX_Images_ColorPalette_pending", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Episodes_ColorPalette_pending", table: "Episodes");

            migrationBuilder.DropIndex(
                name: "IX_Collections_ColorPalette_pending",
                table: "Collections"
            );

            migrationBuilder.DropIndex(name: "IX_Artists_ColorPalette_pending", table: "Artists");

            migrationBuilder.DropIndex(name: "IX_Albums_ColorPalette_pending", table: "Albums");
        }
    }
}
