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

#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchProviderMedia_WatchProviderId",
                table: "WatchProviderMedia"
            );

            migrationBuilder.CreateIndex(
                name: "IX_WatchProviderMedia_WatchProviderId_CountryCode_ProviderType_MovieId_TvId",
                table: "WatchProviderMedia",
                columns: new[]
                {
                    "WatchProviderId",
                    "CountryCode",
                    "ProviderType",
                    "MovieId",
                    "TvId",
                },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchProviderMedia_WatchProviderId_CountryCode_ProviderType_MovieId_TvId",
                table: "WatchProviderMedia"
            );

            migrationBuilder.CreateIndex(
                name: "IX_WatchProviderMedia_WatchProviderId",
                table: "WatchProviderMedia",
                column: "WatchProviderId"
            );
        }
    }
}
