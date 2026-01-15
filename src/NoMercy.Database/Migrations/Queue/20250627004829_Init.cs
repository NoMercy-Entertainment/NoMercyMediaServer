using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations.Queue
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    Connection = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Queue = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Exception = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FailedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueueJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Queue = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Attempts = table.Column<byte>(type: "INTEGER", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AvailableAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueJobs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedJobs");

            migrationBuilder.DropTable(
                name: "QueueJobs");
        }
    }
}
