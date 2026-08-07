using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations.Queue
{
    /// <inheritdoc />
    public partial class QueueJobPayloadHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueJobs_Payload",
                table: "QueueJobs");

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "QueueJobs",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_QueueJobs_PayloadHash",
                table: "QueueJobs",
                column: "PayloadHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueJobs_PayloadHash",
                table: "QueueJobs");

            migrationBuilder.DropColumn(
                name: "PayloadHash",
                table: "QueueJobs");

            migrationBuilder.CreateIndex(
                name: "IX_QueueJobs_Payload",
                table: "QueueJobs",
                column: "Payload");
        }
    }
}
