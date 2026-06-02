using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class ActivityLogCascadeOnDeviceDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Devices_DeviceId",
                table: "ActivityLogs"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Devices_DeviceId",
                table: "ActivityLogs",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Devices_DeviceId",
                table: "ActivityLogs"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Devices_DeviceId",
                table: "ActivityLogs",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }
    }
}
