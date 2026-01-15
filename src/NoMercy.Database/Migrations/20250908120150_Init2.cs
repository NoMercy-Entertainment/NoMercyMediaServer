using System;
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
            migrationBuilder.CreateTable(
                name: "WatchProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    LogoPath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DisplayPriority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WatchProviderMedia",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WatchProviderId = table.Column<int>(type: "INTEGER", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Link = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchProviderMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchProviderMedia_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchProviderMedia_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchProviderMedia_WatchProviders_WatchProviderId",
                        column: x => x.WatchProviderId,
                        principalTable: "WatchProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchProviderMedia_MovieId",
                table: "WatchProviderMedia",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchProviderMedia_TvId",
                table: "WatchProviderMedia",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchProviderMedia_WatchProviderId",
                table: "WatchProviderMedia",
                column: "WatchProviderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchProviderMedia");

            migrationBuilder.DropTable(
                name: "WatchProviders");
        }
    }
}
