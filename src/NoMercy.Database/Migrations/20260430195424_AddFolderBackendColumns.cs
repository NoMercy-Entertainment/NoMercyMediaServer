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
    public partial class AddFolderBackendColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackendConfig",
                table: "Folders",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "BackendType",
                table: "Folders",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "local"
            );

            migrationBuilder.Sql(
                "UPDATE Folders SET BackendType = 'local' WHERE BackendType IS NULL OR BackendType = '';"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_BackendType",
                table: "Folders",
                column: "BackendType"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Folders_BackendType", table: "Folders");

            migrationBuilder.DropColumn(name: "BackendConfig", table: "Folders");

            migrationBuilder.DropColumn(name: "BackendType", table: "Folders");
        }
    }
}
