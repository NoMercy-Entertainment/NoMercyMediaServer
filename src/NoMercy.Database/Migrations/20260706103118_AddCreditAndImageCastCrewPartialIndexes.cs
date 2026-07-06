using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditAndImageCastCrewPartialIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_GuestStarId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Images_CastId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_CrewId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Crews_EpisodeId",
                table: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Crews_MovieId",
                table: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Crews_SeasonId",
                table: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Crews_TvId",
                table: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Casts_EpisodeId",
                table: "Casts");

            migrationBuilder.DropIndex(
                name: "IX_Casts_MovieId",
                table: "Casts");

            migrationBuilder.DropIndex(
                name: "IX_Casts_SeasonId",
                table: "Casts");

            migrationBuilder.DropIndex(
                name: "IX_Casts_TvId",
                table: "Casts");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_GuestStarId",
                table: "Roles",
                column: "GuestStarId",
                unique: true,
                filter: "GuestStarId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CastId",
                table: "Images",
                column: "CastId",
                filter: "CastId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CrewId",
                table: "Images",
                column: "CrewId",
                filter: "CrewId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_EpisodeId",
                table: "Crews",
                column: "EpisodeId",
                filter: "EpisodeId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_MovieId",
                table: "Crews",
                column: "MovieId",
                filter: "MovieId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_SeasonId",
                table: "Crews",
                column: "SeasonId",
                filter: "SeasonId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_TvId",
                table: "Crews",
                column: "TvId",
                filter: "TvId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_EpisodeId",
                table: "Casts",
                column: "EpisodeId",
                filter: "EpisodeId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_MovieId",
                table: "Casts",
                column: "MovieId",
                filter: "MovieId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_SeasonId",
                table: "Casts",
                column: "SeasonId",
                filter: "SeasonId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_TvId",
                table: "Casts",
                column: "TvId",
                filter: "TvId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_GuestStarId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Images_CastId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_CrewId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Crews_EpisodeId",
                table: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Crews_MovieId",
                table: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Crews_SeasonId",
                table: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Crews_TvId",
                table: "Crews");

            migrationBuilder.DropIndex(
                name: "IX_Casts_EpisodeId",
                table: "Casts");

            migrationBuilder.DropIndex(
                name: "IX_Casts_MovieId",
                table: "Casts");

            migrationBuilder.DropIndex(
                name: "IX_Casts_SeasonId",
                table: "Casts");

            migrationBuilder.DropIndex(
                name: "IX_Casts_TvId",
                table: "Casts");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_GuestStarId",
                table: "Roles",
                column: "GuestStarId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_CastId",
                table: "Images",
                column: "CastId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CrewId",
                table: "Images",
                column: "CrewId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_EpisodeId",
                table: "Crews",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_MovieId",
                table: "Crews",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_SeasonId",
                table: "Crews",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_TvId",
                table: "Crews",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_EpisodeId",
                table: "Casts",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_MovieId",
                table: "Casts",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_SeasonId",
                table: "Casts",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_TvId",
                table: "Casts",
                column: "TvId");
        }
    }
}
