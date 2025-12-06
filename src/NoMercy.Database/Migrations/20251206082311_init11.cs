using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class init11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AudioScore",
                table: "ServerCapabilityCache",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CapabilityScoreJson",
                table: "ServerCapabilityCache",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GpuTierScore",
                table: "ServerCapabilityCache",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HardwareAccelerationScore",
                table: "ServerCapabilityCache",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallScore",
                table: "ServerCapabilityCache",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VideoScore",
                table: "ServerCapabilityCache",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioScore",
                table: "ServerCapabilityCache");

            migrationBuilder.DropColumn(
                name: "CapabilityScoreJson",
                table: "ServerCapabilityCache");

            migrationBuilder.DropColumn(
                name: "GpuTierScore",
                table: "ServerCapabilityCache");

            migrationBuilder.DropColumn(
                name: "HardwareAccelerationScore",
                table: "ServerCapabilityCache");

            migrationBuilder.DropColumn(
                name: "OverallScore",
                table: "ServerCapabilityCache");

            migrationBuilder.DropColumn(
                name: "VideoScore",
                table: "ServerCapabilityCache");
        }
    }
}
