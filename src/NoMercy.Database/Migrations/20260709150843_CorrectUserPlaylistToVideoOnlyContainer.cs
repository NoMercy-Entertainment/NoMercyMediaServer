using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class CorrectUserPlaylistToVideoOnlyContainer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistItems_Playlists_PlaylistId",
                table: "PlaylistItems"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistItems_Tracks_TrackId",
                table: "PlaylistItems"
            );

            migrationBuilder.DropIndex(name: "IX_PlaylistItems_TrackId", table: "PlaylistItems");

            migrationBuilder.DropColumn(name: "TrackId", table: "PlaylistItems");

            migrationBuilder.RenameColumn(
                name: "PlaylistId",
                table: "PlaylistItems",
                newName: "UserPlaylistId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_PlaylistItems_PlaylistId_Order",
                table: "PlaylistItems",
                newName: "IX_PlaylistItems_UserPlaylistId_Order"
            );

            migrationBuilder.CreateTable(
                name: "UserPlaylists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4096,
                        nullable: true
                    ),
                    Cover = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlaylists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPlaylists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_UserPlaylists_UserId",
                table: "UserPlaylists",
                column: "UserId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistItems_UserPlaylists_UserPlaylistId",
                table: "PlaylistItems",
                column: "UserPlaylistId",
                principalTable: "UserPlaylists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistItems_UserPlaylists_UserPlaylistId",
                table: "PlaylistItems"
            );

            migrationBuilder.DropTable(name: "UserPlaylists");

            migrationBuilder.RenameColumn(
                name: "UserPlaylistId",
                table: "PlaylistItems",
                newName: "PlaylistId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_PlaylistItems_UserPlaylistId_Order",
                table: "PlaylistItems",
                newName: "IX_PlaylistItems_PlaylistId_Order"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "TrackId",
                table: "PlaylistItems",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_TrackId",
                table: "PlaylistItems",
                column: "TrackId",
                filter: "TrackId IS NOT NULL"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistItems_Playlists_PlaylistId",
                table: "PlaylistItems",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistItems_Tracks_TrackId",
                table: "PlaylistItems",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }
    }
}
