using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddImageForeignKeyPartialIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Images_AlbumId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_ArtistId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_CastCreditId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_CollectionId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_CrewCreditId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_EpisodeId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_MovieId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_PersonId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_SeasonId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_TrackId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_TvId", table: "Images");

            migrationBuilder.CreateIndex(
                name: "IX_Images_AlbumId",
                table: "Images",
                column: "AlbumId",
                filter: "AlbumId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_ArtistId",
                table: "Images",
                column: "ArtistId",
                filter: "ArtistId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_CastCreditId",
                table: "Images",
                column: "CastCreditId",
                filter: "CastCreditId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_CollectionId",
                table: "Images",
                column: "CollectionId",
                filter: "CollectionId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_CrewCreditId",
                table: "Images",
                column: "CrewCreditId",
                filter: "CrewCreditId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_EpisodeId",
                table: "Images",
                column: "EpisodeId",
                filter: "EpisodeId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_MovieId",
                table: "Images",
                column: "MovieId",
                filter: "MovieId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_PersonId",
                table: "Images",
                column: "PersonId",
                filter: "PersonId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_SeasonId",
                table: "Images",
                column: "SeasonId",
                filter: "SeasonId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_TrackId",
                table: "Images",
                column: "TrackId",
                filter: "TrackId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_TvId",
                table: "Images",
                column: "TvId",
                filter: "TvId IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Images_AlbumId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_ArtistId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_CastCreditId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_CollectionId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_CrewCreditId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_EpisodeId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_MovieId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_PersonId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_SeasonId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_TrackId", table: "Images");

            migrationBuilder.DropIndex(name: "IX_Images_TvId", table: "Images");

            migrationBuilder.CreateIndex(
                name: "IX_Images_AlbumId",
                table: "Images",
                column: "AlbumId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_ArtistId",
                table: "Images",
                column: "ArtistId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_CastCreditId",
                table: "Images",
                column: "CastCreditId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_CollectionId",
                table: "Images",
                column: "CollectionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_CrewCreditId",
                table: "Images",
                column: "CrewCreditId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_EpisodeId",
                table: "Images",
                column: "EpisodeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_MovieId",
                table: "Images",
                column: "MovieId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_PersonId",
                table: "Images",
                column: "PersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_SeasonId",
                table: "Images",
                column: "SeasonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Images_TrackId",
                table: "Images",
                column: "TrackId"
            );

            migrationBuilder.CreateIndex(name: "IX_Images_TvId", table: "Images", column: "TvId");
        }
    }
}
