using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations.Queue
{
    /// <inheritdoc />
    public partial class AddEncoderV2Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create EncoderNodes table (no dependencies)
            migrationBuilder.CreateTable(
                name: "EncoderNodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 7627),
                    HasGpu = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    GpuModel = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    GpuVendor = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CpuCores = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    MemoryGb = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    IsHealthy = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastHeartbeat = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MaxConcurrentTasks = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    CurrentTaskCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    SupportedAccelerations = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false, defaultValue: "[]"),
                    OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    FfmpegVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncoderNodes", x => x.Id);
                });

            // Create EncodingJobs table
            // Note: ProfileId references EncoderProfile in MediaContext (different database)
            // Foreign key constraint cannot be enforced at DB level - handled at application level
            migrationBuilder.CreateTable(
                name: "EncodingJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    ProfileSnapshot = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false, defaultValue: ""),
                    InputFilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    OutputFolder = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "queued"),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodingJobs", x => x.Id);
                });

            // Create EncodingTasks table (depends on EncodingJobs and EncoderNodes)
            migrationBuilder.CreateTable(
                name: "EncodingTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    JobId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "pending"),
                    AssignedNodeId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    Dependencies = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false, defaultValue: "[]"),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 3),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    CommandArgs = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false, defaultValue: "{}"),
                    OutputFile = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodingTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncodingTasks_EncodingJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "EncodingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EncodingTasks_EncoderNodes_AssignedNodeId",
                        column: x => x.AssignedNodeId,
                        principalTable: "EncoderNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Create EncodingProgress table (depends on EncodingTasks)
            migrationBuilder.CreateTable(
                name: "EncodingProgress",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    ProgressPercentage = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    Fps = table.Column<double>(type: "REAL", nullable: true),
                    Speed = table.Column<double>(type: "REAL", nullable: true),
                    Bitrate = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CurrentTime = table.Column<long>(type: "INTEGER", nullable: true),
                    TotalDuration = table.Column<long>(type: "INTEGER", nullable: true),
                    EstimatedRemaining = table.Column<long>(type: "INTEGER", nullable: true),
                    EncodedFrames = table.Column<long>(type: "INTEGER", nullable: true),
                    TotalFrames = table.Column<long>(type: "INTEGER", nullable: true),
                    OutputSize = table.Column<long>(type: "INTEGER", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodingProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncodingProgress_EncodingTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "EncodingTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create indexes for EncoderNodes
            migrationBuilder.CreateIndex(
                name: "IX_EncoderNodes_Name",
                table: "EncoderNodes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncoderNodes_IsHealthy_IsEnabled",
                table: "EncoderNodes",
                columns: new[] { "IsHealthy", "IsEnabled" });

            // Create indexes for EncodingJobs
            migrationBuilder.CreateIndex(
                name: "IX_EncodingJobs_State",
                table: "EncodingJobs",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingJobs_Priority",
                table: "EncodingJobs",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingJobs_State_Priority",
                table: "EncodingJobs",
                columns: new[] { "State", "Priority" });

            // Create indexes for EncodingTasks
            migrationBuilder.CreateIndex(
                name: "IX_EncodingTasks_JobId",
                table: "EncodingTasks",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingTasks_State",
                table: "EncodingTasks",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingTasks_AssignedNodeId",
                table: "EncodingTasks",
                column: "AssignedNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingTasks_JobId_State",
                table: "EncodingTasks",
                columns: new[] { "JobId", "State" });

            // Create indexes for EncodingProgress
            migrationBuilder.CreateIndex(
                name: "IX_EncodingProgress_TaskId",
                table: "EncodingProgress",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingProgress_TaskId_RecordedAt",
                table: "EncodingProgress",
                columns: new[] { "TaskId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EncodingProgress");
            migrationBuilder.DropTable(name: "EncodingTasks");
            migrationBuilder.DropTable(name: "EncodingJobs");
            migrationBuilder.DropTable(name: "EncoderNodes");
        }
    }
}
