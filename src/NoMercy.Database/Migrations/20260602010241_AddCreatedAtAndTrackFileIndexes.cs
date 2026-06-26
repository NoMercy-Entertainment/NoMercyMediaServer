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
    public partial class AddCreatedAtAndTrackFileIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tvs_CreatedAt",
                table: "Tvs",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tvs_LibraryId_CreatedAt",
                table: "Tvs",
                columns: new[] { "LibraryId", "CreatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Filename_HostFolder",
                table: "Tracks",
                columns: new[] { "Filename", "HostFolder" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Movies_CreatedAt",
                table: "Movies",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Movies_LibraryId_CreatedAt",
                table: "Movies",
                columns: new[] { "LibraryId", "CreatedAt" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Tvs_CreatedAt", table: "Tvs");

            migrationBuilder.DropIndex(name: "IX_Tvs_LibraryId_CreatedAt", table: "Tvs");

            migrationBuilder.DropIndex(name: "IX_Tracks_Filename_HostFolder", table: "Tracks");

            migrationBuilder.DropIndex(name: "IX_Movies_CreatedAt", table: "Movies");

            migrationBuilder.DropIndex(name: "IX_Movies_LibraryId_CreatedAt", table: "Movies");
        }
    }
}
