using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class MakeDriverIdRequiredAndSeedLocalDrivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1 — seed: for every Folder that still has no DriverId, create a
            // local Driver whose Id equals the Folder's Id (deterministic — no ULID
            // generation needed in SQL), then back-fill DriverId and clear Path
            // (Path becomes the sub-path inside the driver root; "" = driver root).
            migrationBuilder.Sql(
                """
                INSERT OR IGNORE INTO Drivers (Id, Name, Type, Config, CreatedAt, UpdatedAt)
                SELECT
                    f.Id,
                    f.Path,
                    'local',
                    json_object('rootPath', f.Path),
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM Folders f
                WHERE f.DriverId IS NULL;
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE Folders
                SET DriverId = Id,
                    Path     = ''
                WHERE DriverId IS NULL;
                """
            );

            // Step 2 — schema: drop the old nullable FK + its foreign-key constraint,
            // then recreate the column as NOT NULL with a Restrict delete rule.
            // SQLite does not support ALTER COLUMN, so we use the standard
            // table-rebuild pattern.

            migrationBuilder.DropForeignKey(name: "FK_Folders_Drivers_DriverId", table: "Folders");

            migrationBuilder.DropIndex(name: "IX_Folders_DriverId", table: "Folders");

            migrationBuilder.AlterColumn<string>(
                name: "DriverId",
                table: "Folders",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DriverId",
                table: "Folders",
                column: "DriverId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Drivers_DriverId",
                table: "Folders",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Folders_Drivers_DriverId", table: "Folders");

            migrationBuilder.DropIndex(name: "IX_Folders_DriverId", table: "Folders");

            migrationBuilder.AlterColumn<string>(
                name: "DriverId",
                table: "Folders",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: false
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DriverId",
                table: "Folders",
                column: "DriverId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Drivers_DriverId",
                table: "Folders",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }
    }
}
