#nullable disable

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEncodeTaskOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncodeTaskOutcomes",
                columns: table => new
                {
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ParentJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupTag = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4096,
                        nullable: true
                    ),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OutputArtifactsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodeTaskOutcomes", x => x.TaskId);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EncodeTaskOutcomes_GroupTag",
                table: "EncodeTaskOutcomes",
                column: "GroupTag"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EncodeTaskOutcomes_ParentJobId",
                table: "EncodeTaskOutcomes",
                column: "ParentJobId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EncodeTaskOutcomes");
        }
    }
}
