using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEncodingPresetTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncodingPresets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Author = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ProfileJson = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ParentPresetId = table.Column<string>(type: "TEXT", nullable: true),
                    ParentId = table.Column<string>(type: "TEXT", nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodingPresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncodingPresets_EncodingPresets_ParentId",
                        column: x => x.ParentId,
                        principalTable: "EncodingPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncodingPresets_Name",
                table: "EncodingPresets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingPresets_ParentId",
                table: "EncodingPresets",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingPresets_ParentPresetId",
                table: "EncodingPresets",
                column: "ParentPresetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncodingPresets");
        }
    }
}
