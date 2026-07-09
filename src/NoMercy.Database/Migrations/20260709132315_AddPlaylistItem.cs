using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SpecialId = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_PlaylistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Specials_SpecialId",
                        column: x => x.SpecialId,
                        principalTable: "Specials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_EpisodeId",
                table: "PlaylistItems",
                column: "EpisodeId",
                filter: "EpisodeId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_MovieId",
                table: "PlaylistItems",
                column: "MovieId",
                filter: "MovieId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId_Order",
                table: "PlaylistItems",
                columns: new[] { "PlaylistId", "Order" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_SpecialId",
                table: "PlaylistItems",
                column: "SpecialId",
                filter: "SpecialId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_TrackId",
                table: "PlaylistItems",
                column: "TrackId",
                filter: "TrackId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_TvId",
                table: "PlaylistItems",
                column: "TvId",
                filter: "TvId IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlaylistItems");
        }
    }
}
