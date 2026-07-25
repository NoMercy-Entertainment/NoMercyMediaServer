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

namespace NoMercy.Database.Migrations.Queue
{
    /// <inheritdoc />
    public partial class Init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "CronJobs",
                columns: table => new
                {
                    Id = table
                        .Column<int>("INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>("TEXT", maxLength: 256, nullable: false),
                    CronExpression = table.Column<string>(
                        "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    JobType = table.Column<string>("TEXT", maxLength: 256, nullable: false),
                    Parameters = table.Column<string>("TEXT", maxLength: 256, nullable: true),
                    IsEnabled = table.Column<bool>("INTEGER", nullable: false),
                    LastRun = table.Column<DateTime>("TEXT", nullable: true),
                    NextRun = table.Column<DateTime>("TEXT", nullable: true),
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
                    table.PrimaryKey("PK_CronJobs", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                "IX_CronJobs_Name",
                table: "CronJobs",
                column: "Name",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("CronJobs");
        }
    }
}
