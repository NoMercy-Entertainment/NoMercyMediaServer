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
    public partial class AddDriversTableAndFolderDriverId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Config = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(
                        type: "TEXT",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    UpdatedAt = table.Column<string>(
                        type: "TEXT",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Name",
                table: "Drivers",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(name: "IX_Drivers_Type", table: "Drivers", column: "Type");

            // Drop the old per-folder driver columns.
            migrationBuilder.DropIndex(name: "IX_Folders_DriverType", table: "Folders");

            migrationBuilder.DropColumn(name: "DriverType", table: "Folders");

            migrationBuilder.DropColumn(name: "DriverConfig", table: "Folders");

            // Add the FK column. NULL means built-in local.
            migrationBuilder.AddColumn<string>(
                name: "DriverId",
                table: "Folders",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DriverId",
                table: "Folders",
                column: "DriverId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Drivers_DriverId",
                table: "Folders",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Folders_Drivers_DriverId", table: "Folders");

            migrationBuilder.DropIndex(name: "IX_Folders_DriverId", table: "Folders");

            migrationBuilder.DropColumn(name: "DriverId", table: "Folders");

            migrationBuilder.AddColumn<string>(
                name: "DriverType",
                table: "Folders",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "local"
            );

            migrationBuilder.AddColumn<string>(
                name: "DriverConfig",
                table: "Folders",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DriverType",
                table: "Folders",
                column: "DriverType"
            );

            migrationBuilder.DropIndex(name: "IX_Drivers_Name", table: "Drivers");

            migrationBuilder.DropIndex(name: "IX_Drivers_Type", table: "Drivers");

            migrationBuilder.DropTable(name: "Drivers");
        }
    }
}
