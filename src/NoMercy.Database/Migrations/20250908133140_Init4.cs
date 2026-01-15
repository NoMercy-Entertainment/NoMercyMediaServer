using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchProviderMedia_WatchProviderId",
                table: "WatchProviderMedia");

            migrationBuilder.CreateIndex(
                name: "IX_WatchProviderMedia_WatchProviderId_CountryCode_ProviderType_MovieId_TvId",
                table: "WatchProviderMedia",
                columns: new[] { "WatchProviderId", "CountryCode", "ProviderType", "MovieId", "TvId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchProviderMedia_WatchProviderId_CountryCode_ProviderType_MovieId_TvId",
                table: "WatchProviderMedia");

            migrationBuilder.CreateIndex(
                name: "IX_WatchProviderMedia_WatchProviderId",
                table: "WatchProviderMedia",
                column: "WatchProviderId");
        }
    }
}
