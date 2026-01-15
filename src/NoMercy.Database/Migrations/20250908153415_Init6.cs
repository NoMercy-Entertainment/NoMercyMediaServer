using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriorityProvider");

            migrationBuilder.DropTable(
                name: "Providers");

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Headquarters = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Homepage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LogoPath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OriginCountry = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ParentCompany = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Networks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Logo = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OriginCountry = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Networks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyMovie",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyMovie", x => new { x.CompanyId, x.MovieId });
                    table.ForeignKey(
                        name: "FK_CompanyMovie_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyTv",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyTv", x => new { x.CompanyId, x.TvId });
                    table.ForeignKey(
                        name: "FK_CompanyTv_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTv",
                columns: table => new
                {
                    NetworkId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTv", x => new { x.NetworkId, x.TvId });
                    table.ForeignKey(
                        name: "FK_NetworkTv_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMovie_CompanyId_MovieId",
                table: "CompanyMovie",
                columns: new[] { "CompanyId", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMovie_MovieId",
                table: "CompanyMovie",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTv_CompanyId_TvId",
                table: "CompanyTv",
                columns: new[] { "CompanyId", "TvId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTv_TvId",
                table: "CompanyTv",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_Networks_Name",
                table: "Networks",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTv_NetworkId_TvId",
                table: "NetworkTv",
                columns: new[] { "NetworkId", "TvId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTv_TvId",
                table: "NetworkTv",
                column: "TvId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyMovie");

            migrationBuilder.DropTable(
                name: "CompanyTv");

            migrationBuilder.DropTable(
                name: "NetworkTv");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Networks");

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriorityProvider",
                columns: table => new
                {
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProviderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorityProvider", x => new { x.Priority, x.Country, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_PriorityProvider_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriorityProvider_Country",
                table: "PriorityProvider",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_PriorityProvider_Priority",
                table: "PriorityProvider",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_PriorityProvider_ProviderId",
                table: "PriorityProvider",
                column: "ProviderId");
        }
    }
}
