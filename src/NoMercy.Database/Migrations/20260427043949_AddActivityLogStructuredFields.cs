#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLogStructuredFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "ActivityLogs",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "ActivityLogs",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "MediaId",
                table: "ActivityLogs",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "ActivityLogs",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "ActivityLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.Sql(
                @"UPDATE ActivityLogs
                  SET Category = 2,
                      Type = CASE
                          WHEN Type = 'Connected to server' THEN 'connection.connected'
                          WHEN Type = 'Disconnected from server' THEN 'connection.disconnected'
                          ELSE Type
                      END,
                      Success = 1
                  WHERE Category IS NULL OR Category = 0;"
            );

            migrationBuilder.AlterColumn<int>(
                name: "Category",
                table: "ActivityLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Category",
                table: "ActivityLogs",
                column: "Category"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_MediaId",
                table: "ActivityLogs",
                column: "MediaId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_ActivityLogs_Category", table: "ActivityLogs");

            migrationBuilder.DropIndex(name: "IX_ActivityLogs_MediaId", table: "ActivityLogs");

            migrationBuilder.DropColumn(name: "Category", table: "ActivityLogs");

            migrationBuilder.DropColumn(name: "ErrorCode", table: "ActivityLogs");

            migrationBuilder.DropColumn(name: "MediaId", table: "ActivityLogs");

            migrationBuilder.DropColumn(name: "Metadata", table: "ActivityLogs");

            migrationBuilder.DropColumn(name: "Success", table: "ActivityLogs");
        }
    }
}
