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

namespace NoMercy.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialAppSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "Configuration",
                columns: table => new
                {
                    Id = table
                        .Column<int>("INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>("TEXT", nullable: false),
                    Value = table.Column<string>("TEXT", nullable: false),
                    ModifiedBy = table.Column<Guid>("TEXT", nullable: true),
                    SecureValue = table.Column<string>("TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(
                        "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuration", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                "IX_Configuration_Key",
                table: "Configuration",
                column: "Key",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Configuration");
        }
    }
}
