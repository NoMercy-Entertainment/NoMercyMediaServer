using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations.Queue
{
    /// <inheritdoc />
    public partial class AddQueueJobReserveIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_QueueJobs_Queue_ReservedAt_Priority_CreatedAt_Id",
                table: "QueueJobs",
                columns: new[] { "Queue", "ReservedAt", "Priority", "CreatedAt", "Id" },
                descending: new[] { false, false, true, false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueJobs_Queue_ReservedAt_Priority_CreatedAt_Id",
                table: "QueueJobs");
        }
    }
}
