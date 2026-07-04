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
    public partial class FixTrackMetadataCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metadata_Tracks_AudioTrackId",
                table: "Metadata"
            );

            migrationBuilder.DropForeignKey(name: "FK_Tracks_Metadata_MetadataId", table: "Tracks");

            migrationBuilder.AddForeignKey(
                name: "FK_Metadata_Tracks_AudioTrackId",
                table: "Metadata",
                column: "AudioTrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Metadata_MetadataId",
                table: "Tracks",
                column: "MetadataId",
                principalTable: "Metadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metadata_Tracks_AudioTrackId",
                table: "Metadata"
            );

            migrationBuilder.DropForeignKey(name: "FK_Tracks_Metadata_MetadataId", table: "Tracks");

            migrationBuilder.AddForeignKey(
                name: "FK_Metadata_Tracks_AudioTrackId",
                table: "Metadata",
                column: "AudioTrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Metadata_MetadataId",
                table: "Tracks",
                column: "MetadataId",
                principalTable: "Metadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
