#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEncodingHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncodingHistory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    InputPath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4096,
                        nullable: false
                    ),
                    OutputPath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 4096,
                        nullable: false
                    ),
                    ProfileId = table.Column<string>(type: "TEXT", nullable: true),
                    ProfileName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    EncoderUsed = table.Column<string>(
                        type: "TEXT",
                        maxLength: 64,
                        nullable: false
                    ),
                    GpuUsed = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    InputSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    OutputSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CompressionRatio = table.Column<double>(type: "REAL", nullable: false),
                    AverageSpeed = table.Column<double>(type: "REAL", nullable: false),
                    AverageFps = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodingHistory", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EncodingHistory_CreatedAt",
                table: "EncodingHistory",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EncodingHistory_ProfileId",
                table: "EncodingHistory",
                column: "ProfileId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EncodingHistory");
        }
    }
}
