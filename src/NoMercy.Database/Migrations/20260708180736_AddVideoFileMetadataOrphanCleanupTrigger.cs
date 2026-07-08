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
    public partial class AddVideoFileMetadataOrphanCleanupTrigger : Migration
    {
        // VideoFile references Metadata (VideoFile.MetadataId -> Metadata.Id), so an
        // FK cascade can only run Metadata -> VideoFile, never the reverse. Metadata is
        // also shared-reference data (a Track or another VideoFile may point at the same
        // row), which is why that FK is deliberately Restrict. The result: deleting a
        // VideoFile leaves its Metadata orphaned unless the calling code cleans it up
        // (only the Movie/Tv rescan paths do). This trigger closes the gap for every
        // delete path — EF ExecuteDelete, tracked SaveChanges, or a raw row delete — and
        // removes the Metadata only once nothing else references it, so shared rows and
        // the Restrict guards are respected.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TRIGGER "delete_orphan_metadata_after_videofile_delete"
                AFTER DELETE ON "VideoFiles"
                FOR EACH ROW
                WHEN OLD."MetadataId" IS NOT NULL
                BEGIN
                    DELETE FROM "Metadata"
                    WHERE "Id" = OLD."MetadataId"
                      AND NOT EXISTS (
                          SELECT 1 FROM "VideoFiles" WHERE "MetadataId" = OLD."MetadataId"
                      )
                      AND NOT EXISTS (
                          SELECT 1 FROM "Tracks" WHERE "MetadataId" = OLD."MetadataId"
                      );
                END;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP TRIGGER IF EXISTS ""delete_orphan_metadata_after_videofile_delete"";"
            );
        }
    }
}
