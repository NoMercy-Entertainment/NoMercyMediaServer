using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "PlaybackPreferences",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialId",
                table: "PlaybackPreferences",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_CollectionId",
                table: "PlaybackPreferences",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_SpecialId",
                table: "PlaybackPreferences",
                column: "SpecialId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Collections_CollectionId",
                table: "PlaybackPreferences",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Specials_SpecialId",
                table: "PlaybackPreferences",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Collections_CollectionId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Specials_SpecialId",
                table: "PlaybackPreferences");

            migrationBuilder.DropIndex(
                name: "IX_PlaybackPreferences_CollectionId",
                table: "PlaybackPreferences");

            migrationBuilder.DropIndex(
                name: "IX_PlaybackPreferences_SpecialId",
                table: "PlaybackPreferences");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "PlaybackPreferences");

            migrationBuilder.DropColumn(
                name: "SpecialId",
                table: "PlaybackPreferences");
        }
    }
}
