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

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SourcePath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    DriverId = table.Column<string>(type: "TEXT", nullable: false),
                    DetectedType = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    Confidence = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    Status = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Candidates = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2147483647,
                        nullable: false
                    ),
                    SelectedMatch = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2147483647,
                        nullable: true
                    ),
                    TargetLibraryId = table.Column<string>(type: "TEXT", nullable: true),
                    TargetFolderId = table.Column<string>(type: "TEXT", nullable: true),
                    TargetProfileId = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    CreatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxItems", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_InboxItems_CreatedAt",
                table: "InboxItems",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_InboxItems_DetectedType",
                table: "InboxItems",
                column: "DetectedType"
            );

            migrationBuilder.CreateIndex(
                name: "IX_InboxItems_Status",
                table: "InboxItems",
                column: "Status"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InboxItems");
        }
    }
}
