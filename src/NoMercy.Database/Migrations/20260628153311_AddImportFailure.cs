using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddImportFailure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportFailures",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    JobType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ErrorMessage = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4096,
                        nullable: false
                    ),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Resolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_ImportFailures", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImportFailures_CreatedAt",
                table: "ImportFailures",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImportFailures_JobType",
                table: "ImportFailures",
                column: "JobType"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImportFailures_LibraryId",
                table: "ImportFailures",
                column: "LibraryId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImportFailures_Resolved",
                table: "ImportFailures",
                column: "Resolved"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ImportFailures");
        }
    }
}
