#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace NoMercy.Database.Migrations.Queue
{
    /// <inheritdoc />
    public partial class AddJobParentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentJobId",
                table: "QueueJobs",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "GroupTag",
                table: "QueueJobs",
                type: "TEXT",
                maxLength: 64,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ParentJobId", table: "QueueJobs");

            migrationBuilder.DropColumn(name: "GroupTag", table: "QueueJobs");
        }
    }
}
