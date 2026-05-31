using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class EncoderProfilesV2 : Migration
    {
        // V1ToV2 is inlined as an empty dict because NoMercy.Database cannot
        // reference NoMercy.Encoder (cycle: Encoder → Database). The loop
        // shape is preserved for future migrations that populate the map.
        private static readonly IReadOnlyDictionary<Ulid, Ulid> _v1ToV2Renames =
            new Dictionary<Ulid, Ulid>();

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drift-tolerant: IX_VideoFiles_Filename may not exist on every dev DB
            // (prior migration history is inconsistent across machines). DropIndex
            // crashes the runtime if absent; raw SQL with IF EXISTS is safe.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_VideoFiles_Filename\";");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "EncodingPresets",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.CreateTable(
                name: "EncodingPresetFolders",
                columns: table => new
                {
                    PresetId = table.Column<string>(type: "TEXT", nullable: false),
                    FolderId = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_EncodingPresetFolders",
                        x => new { x.PresetId, x.FolderId }
                    );
                    table.ForeignKey(
                        name: "FK_EncodingPresetFolders_EncodingPresets_PresetId",
                        column: x => x.PresetId,
                        principalTable: "EncodingPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_EncodingPresetFolders_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            // Drift-tolerant create: idempotent if a previous run partially applied.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_VideoFiles_Filename_HostFolder\" ON \"VideoFiles\" (\"Filename\", \"HostFolder\");"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EncodingPresets_IsBuiltIn",
                table: "EncodingPresets",
                column: "IsBuiltIn"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EncodingPresets_Source",
                table: "EncodingPresets",
                column: "Source"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EncodingPresetFolders_FolderId",
                table: "EncodingPresetFolders",
                column: "FolderId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EncodingPresetFolders_PresetId_IsDefault",
                table: "EncodingPresetFolders",
                columns: new[] { "PresetId", "IsDefault" }
            );

            // Step 2 audit: no table outside the V1 junction (EncoderProfileFolder)
            // carries an EncoderProfileId FK, so no orphan-null block is needed here.
            // V1 tables themselves are dropped in Phase H.

            // Apply BuiltinPresetRenames: empty for initial v2 release.
            foreach (KeyValuePair<Ulid, Ulid> rename in _v1ToV2Renames)
            {
                string oldId = rename.Key.ToString();
                string newId = rename.Value.ToString();
                migrationBuilder.Sql(
                    $"UPDATE \"EncodingPresetFolders\" SET \"PresetId\" = '{newId}' WHERE \"PresetId\" = '{oldId}';"
                );
                migrationBuilder.Sql(
                    $"DELETE FROM \"EncodingPresets\" WHERE \"Id\" = '{oldId}' AND \"IsBuiltIn\" = 1;"
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EncodingPresetFolders");

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_VideoFiles_Filename_HostFolder\";");

            migrationBuilder.DropIndex(
                name: "IX_EncodingPresets_IsBuiltIn",
                table: "EncodingPresets"
            );

            migrationBuilder.DropIndex(name: "IX_EncodingPresets_Source", table: "EncodingPresets");

            migrationBuilder.DropColumn(name: "Source", table: "EncodingPresets");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_VideoFiles_Filename\" ON \"VideoFiles\" (\"Filename\");"
            );
        }
    }
}
