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
    public partial class AddDevicePresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "LanIp",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "LanPort",
                table: "Devices",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "MdnsSeenAt",
                table: "Devices",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Devices",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "WsConnectedAt",
                table: "Devices",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OwnerUserId_Fingerprint",
                table: "Devices",
                columns: new[] { "OwnerUserId", "Fingerprint" },
                unique: true,
                filter: "Fingerprint IS NOT NULL"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Users_OwnerUserId",
                table: "Devices",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Devices_Users_OwnerUserId", table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_OwnerUserId_Fingerprint",
                table: "Devices"
            );

            migrationBuilder.DropColumn(name: "Fingerprint", table: "Devices");

            migrationBuilder.DropColumn(name: "LanIp", table: "Devices");

            migrationBuilder.DropColumn(name: "LanPort", table: "Devices");

            migrationBuilder.DropColumn(name: "MdnsSeenAt", table: "Devices");

            migrationBuilder.DropColumn(name: "OwnerUserId", table: "Devices");

            migrationBuilder.DropColumn(name: "WsConnectedAt", table: "Devices");
        }
    }
}
