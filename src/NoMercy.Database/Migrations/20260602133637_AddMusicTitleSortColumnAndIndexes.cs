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
    public partial class AddMusicTitleSortColumnAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TitleSort",
                table: "Albums",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Artists_TitleSort",
                table: "Artists",
                column: "TitleSort"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Albums_TitleSort",
                table: "Albums",
                column: "TitleSort"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Artists_TitleSort", table: "Artists");

            migrationBuilder.DropIndex(name: "IX_Albums_TitleSort", table: "Albums");

            migrationBuilder.DropColumn(name: "TitleSort", table: "Albums");
        }
    }
}
