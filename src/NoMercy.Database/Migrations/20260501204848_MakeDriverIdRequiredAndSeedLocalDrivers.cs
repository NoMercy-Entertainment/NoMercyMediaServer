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
            // Step 0 — drop the old unique-Path index. In the new model Path is a
            // sub-path inside the driver root, so two folders on different drivers
            // can legitimately share the same sub-path (e.g. "" = driver root, or
            // "Films"). Replaced after seeding by a composite unique on
            // (DriverId, Path) so a single driver still can't have two folders at
            // the same sub-path.
            migrationBuilder.DropIndex(name: "IX_Folders_Path", table: "Folders");

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

            // Composite unique index on (DriverId, Path) — also serves as the
            // FK-supporting index, so no separate IX_Folders_DriverId is needed.
            migrationBuilder.CreateIndex(
                name: "IX_Folders_DriverId_Path",
                table: "Folders",
                columns: ["DriverId", "Path"],
                unique: true
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

            migrationBuilder.DropIndex(name: "IX_Folders_DriverId_Path", table: "Folders");

            migrationBuilder.AlterColumn<string>(
                name: "DriverId",
                table: "Folders",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: false
            );

            // Restore Path from the Driver's Config rootPath before recreating the
            // unique index — without this every folder has Path='' and the index fails.
            migrationBuilder.Sql(
                """
                UPDATE Folders
                SET Path = json_extract(d.Config, '$.rootPath')
                FROM Drivers d
                WHERE d.Id = Folders.DriverId;
                """
            );

            // Null out DriverId for auto-seeded drivers (Id == DriverId means Up()
            // created the driver from this folder), then delete those seeded rows.
            migrationBuilder.Sql(
                """
                UPDATE Folders
                SET DriverId = NULL
                WHERE DriverId = Id;
                """
            );

            migrationBuilder.Sql(
                """
                DELETE FROM Drivers
                WHERE Id NOT IN (SELECT DriverId FROM Folders WHERE DriverId IS NOT NULL);
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DriverId",
                table: "Folders",
                column: "DriverId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Folders_Path",
                table: "Folders",
                column: "Path",
                unique: true
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
