using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class init10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerCapabilityCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FfmpegVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    HardwareJson = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GpuDevicesJson = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    VideoCodecsJson = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AudioCodecsJson = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CachedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerCapabilityCache", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerCapabilityCache");
        }
    }
}
