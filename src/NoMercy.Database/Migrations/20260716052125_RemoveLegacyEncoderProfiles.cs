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
    public partial class RemoveLegacyEncoderProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drift-tolerant: the trigger/tables may already be absent on a dev DB
            // that was never fully migrated through every intermediate revision.
            // Raw SQL with IF EXISTS is safe either way; the typed DropTable API
            // throws if the table is already gone.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"update_EncoderProfiles_updated_at\";");

            // Child table first — it FKs into EncoderProfiles.
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"EncoderProfileFolder\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"EncoderProfiles\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The V1 EncoderProfile system (and the seeder round-trip that kept it
            // in sync with EncodingPresets) is gone from the codebase — recreating
            // empty V1 tables here would produce a schema no running code writes
            // to or reads from, which is a false "rollback" that silently loses
            // whatever was in those tables before Up() ran. Honest answer: this
            // migration cannot be undone without restoring from a backup taken
            // before it was applied.
            throw new System.NotSupportedException(
                "RemoveLegacyEncoderProfiles cannot be rolled back — the V1 EncoderProfile "
                    + "tables and the code that read/wrote them have been removed. Restore "
                    + "media.db from a pre-migration backup instead."
            );
        }
    }
}
