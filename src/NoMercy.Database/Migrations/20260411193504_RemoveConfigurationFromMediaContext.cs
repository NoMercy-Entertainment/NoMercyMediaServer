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
    public partial class RemoveConfigurationFromMediaContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF EXISTS for safety — fresh installs never had this table in media.db
            migrationBuilder.Sql("DROP TABLE IF EXISTS Configuration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configuration",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    SecureValue = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: true
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    Value = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuration", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Configuration_Key",
                table: "Configuration",
                column: "Key",
                unique: true
            );
        }
    }
}
