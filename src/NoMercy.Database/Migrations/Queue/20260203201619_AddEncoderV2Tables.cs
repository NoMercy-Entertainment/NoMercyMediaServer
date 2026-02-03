using System;
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
            migrationBuilder.CreateTable(
                name: "EncoderNodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    HasGpu = table.Column<bool>(type: "INTEGER", nullable: false),
                    GpuModel = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    GpuVendor = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CpuCores = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryGb = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHealthy = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MaxConcurrentTasks = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentTaskCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SupportedAccelerations = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FfmpegVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncoderNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncodingJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", nullable: true),
                    ProfileSnapshot = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    InputFilePath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OutputFolder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodingJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncodingTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    JobId = table.Column<string>(type: "TEXT", nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AssignedNodeId = table.Column<string>(type: "TEXT", nullable: true),
                    Dependencies = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CommandArgs = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OutputFile = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodingTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncodingTasks_EncoderNodes_AssignedNodeId",
                        column: x => x.AssignedNodeId,
                        principalTable: "EncoderNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EncodingTasks_EncodingJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "EncodingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EncodingProgress",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", nullable: false),
                    ProgressPercentage = table.Column<double>(type: "REAL", nullable: false),
                    Fps = table.Column<double>(type: "REAL", nullable: true),
                    Speed = table.Column<double>(type: "REAL", nullable: true),
                    Bitrate = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CurrentTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    TotalDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    EstimatedRemaining = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    EncodedFrames = table.Column<long>(type: "INTEGER", nullable: true),
                    TotalFrames = table.Column<long>(type: "INTEGER", nullable: true),
                    OutputSize = table.Column<long>(type: "INTEGER", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_EncodingProgress_TaskId",
                table: "EncodingProgress",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingTasks_AssignedNodeId",
                table: "EncodingTasks",
                column: "AssignedNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_EncodingTasks_JobId",
                table: "EncodingTasks",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncodingProgress");

            migrationBuilder.DropTable(
                name: "EncodingTasks");

            migrationBuilder.DropTable(
                name: "EncoderNodes");

            migrationBuilder.DropTable(
                name: "EncodingJobs");
        }
    }
}
