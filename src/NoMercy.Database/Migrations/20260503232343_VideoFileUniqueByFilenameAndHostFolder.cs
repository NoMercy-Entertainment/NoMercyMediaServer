// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class VideoFileUniqueByFilenameAndHostFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old per-filename unique index first so the dedup DELETE below
            // does not fight it.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_VideoFiles_Filename\";");

            // Dedup (Filename, HostFolder) before the composite unique index, else a
            // prod DB with duplicates fails the index creation and bricks boot.
            // Keeps the earliest row (MIN rowid). No-op on a clean DB.
            migrationBuilder.Sql(
                """
                DELETE FROM VideoFiles
                WHERE rowid NOT IN (
                    SELECT MIN(rowid)
                    FROM VideoFiles
                    GROUP BY Filename, HostFolder
                );
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_Filename_HostFolder",
                table: "VideoFiles",
                columns: new[] { "Filename", "HostFolder" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoFiles_Filename_HostFolder",
                table: "VideoFiles"
            );

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_Filename",
                table: "VideoFiles",
                column: "Filename",
                unique: true
            );
        }
    }
}
