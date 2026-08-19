using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeThemeDemographicSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnimeDemographicId",
                table: "Translations",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "AnimeThemeId",
                table: "Translations",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "AnimeDemographics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeDemographics", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "AnimeSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Quarter = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeSeasons", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "AnimeThemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeThemes", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "AnimeDemographicMovie",
                columns: table => new
                {
                    AnimeDemographicId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_AnimeDemographicMovie",
                        x => new { x.AnimeDemographicId, x.MovieId }
                    );
                    table.ForeignKey(
                        name: "FK_AnimeDemographicMovie_AnimeDemographics_AnimeDemographicId",
                        column: x => x.AnimeDemographicId,
                        principalTable: "AnimeDemographics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AnimeDemographicMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AnimeDemographicTv",
                columns: table => new
                {
                    AnimeDemographicId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_AnimeDemographicTv",
                        x => new { x.AnimeDemographicId, x.TvId }
                    );
                    table.ForeignKey(
                        name: "FK_AnimeDemographicTv_AnimeDemographics_AnimeDemographicId",
                        column: x => x.AnimeDemographicId,
                        principalTable: "AnimeDemographics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AnimeDemographicTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AnimeSeasonMovie",
                columns: table => new
                {
                    AnimeSeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_AnimeSeasonMovie",
                        x => new { x.AnimeSeasonId, x.MovieId }
                    );
                    table.ForeignKey(
                        name: "FK_AnimeSeasonMovie_AnimeSeasons_AnimeSeasonId",
                        column: x => x.AnimeSeasonId,
                        principalTable: "AnimeSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AnimeSeasonMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AnimeSeasonTv",
                columns: table => new
                {
                    AnimeSeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeSeasonTv", x => new { x.AnimeSeasonId, x.TvId });
                    table.ForeignKey(
                        name: "FK_AnimeSeasonTv_AnimeSeasons_AnimeSeasonId",
                        column: x => x.AnimeSeasonId,
                        principalTable: "AnimeSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AnimeSeasonTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AnimeThemeMovie",
                columns: table => new
                {
                    AnimeThemeId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeThemeMovie", x => new { x.AnimeThemeId, x.MovieId });
                    table.ForeignKey(
                        name: "FK_AnimeThemeMovie_AnimeThemes_AnimeThemeId",
                        column: x => x.AnimeThemeId,
                        principalTable: "AnimeThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AnimeThemeMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AnimeThemeTv",
                columns: table => new
                {
                    AnimeThemeId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeThemeTv", x => new { x.AnimeThemeId, x.TvId });
                    table.ForeignKey(
                        name: "FK_AnimeThemeTv_AnimeThemes_AnimeThemeId",
                        column: x => x.AnimeThemeId,
                        principalTable: "AnimeThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AnimeThemeTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Translations_AnimeDemographicId",
                table: "Translations",
                column: "AnimeDemographicId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Translations_AnimeThemeId",
                table: "Translations",
                column: "AnimeThemeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeDemographicMovie_AnimeDemographicId",
                table: "AnimeDemographicMovie",
                column: "AnimeDemographicId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeDemographicMovie_MovieId",
                table: "AnimeDemographicMovie",
                column: "MovieId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeDemographics_Name",
                table: "AnimeDemographics",
                column: "Name"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeDemographicTv_AnimeDemographicId",
                table: "AnimeDemographicTv",
                column: "AnimeDemographicId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeDemographicTv_TvId",
                table: "AnimeDemographicTv",
                column: "TvId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeSeasonMovie_AnimeSeasonId",
                table: "AnimeSeasonMovie",
                column: "AnimeSeasonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeSeasonMovie_MovieId",
                table: "AnimeSeasonMovie",
                column: "MovieId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeSeasons_Quarter",
                table: "AnimeSeasons",
                column: "Quarter"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeSeasons_Year",
                table: "AnimeSeasons",
                column: "Year"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeSeasonTv_AnimeSeasonId",
                table: "AnimeSeasonTv",
                column: "AnimeSeasonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeSeasonTv_TvId",
                table: "AnimeSeasonTv",
                column: "TvId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeThemeMovie_AnimeThemeId",
                table: "AnimeThemeMovie",
                column: "AnimeThemeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeThemeMovie_MovieId",
                table: "AnimeThemeMovie",
                column: "MovieId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeThemes_Name",
                table: "AnimeThemes",
                column: "Name"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeThemeTv_AnimeThemeId",
                table: "AnimeThemeTv",
                column: "AnimeThemeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AnimeThemeTv_TvId",
                table: "AnimeThemeTv",
                column: "TvId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_AnimeDemographics_AnimeDemographicId",
                table: "Translations",
                column: "AnimeDemographicId",
                principalTable: "AnimeDemographics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_AnimeThemes_AnimeThemeId",
                table: "Translations",
                column: "AnimeThemeId",
                principalTable: "AnimeThemes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Translations_AnimeDemographics_AnimeDemographicId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_AnimeThemes_AnimeThemeId",
                table: "Translations"
            );

            migrationBuilder.DropTable(name: "AnimeDemographicMovie");

            migrationBuilder.DropTable(name: "AnimeDemographicTv");

            migrationBuilder.DropTable(name: "AnimeSeasonMovie");

            migrationBuilder.DropTable(name: "AnimeSeasonTv");

            migrationBuilder.DropTable(name: "AnimeThemeMovie");

            migrationBuilder.DropTable(name: "AnimeThemeTv");

            migrationBuilder.DropTable(name: "AnimeDemographics");

            migrationBuilder.DropTable(name: "AnimeSeasons");

            migrationBuilder.DropTable(name: "AnimeThemes");

            migrationBuilder.DropIndex(
                name: "IX_Translations_AnimeDemographicId",
                table: "Translations"
            );

            migrationBuilder.DropIndex(name: "IX_Translations_AnimeThemeId", table: "Translations");

            migrationBuilder.DropColumn(name: "AnimeDemographicId", table: "Translations");

            migrationBuilder.DropColumn(name: "AnimeThemeId", table: "Translations");
        }
    }
}
