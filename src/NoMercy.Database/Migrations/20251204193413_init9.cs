using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class init9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncoderNodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    NodeId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NodeName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NetworkAddress = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NetworkPort = table.Column<int>(type: "INTEGER", nullable: false),
                    UseHttps = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncoderNodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncoderNodes_NodeId",
                table: "EncoderNodes",
                column: "NodeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncoderNodes");
        }
    }
}
