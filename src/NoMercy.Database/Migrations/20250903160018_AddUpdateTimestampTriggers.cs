using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdateTimestampTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // User table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Users_updated_at
                AFTER UPDATE ON Users
                FOR EACH ROW
                BEGIN
                    UPDATE Users SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // ActivityLog table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_ActivityLogs_updated_at
                AFTER UPDATE ON ActivityLogs
                FOR EACH ROW
                BEGIN
                    UPDATE ActivityLogs SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Device table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Devices_updated_at
                AFTER UPDATE ON Devices
                FOR EACH ROW
                BEGIN
                    UPDATE Devices SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Library table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Libraries_updated_at
                AFTER UPDATE ON Libraries
                FOR EACH ROW
                BEGIN
                    UPDATE Libraries SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Movie table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Movies_updated_at
                AFTER UPDATE ON Movies
                FOR EACH ROW
                BEGIN
                    UPDATE Movies SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Tv table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Tvs_updated_at
                AFTER UPDATE ON Tvs
                FOR EACH ROW
                BEGIN
                    UPDATE Tvs SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Season table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Seasons_updated_at
                AFTER UPDATE ON Seasons
                FOR EACH ROW
                BEGIN
                    UPDATE Seasons SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Episode table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Episodes_updated_at
                AFTER UPDATE ON Episodes
                FOR EACH ROW
                BEGIN
                    UPDATE Episodes SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Person table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_People_updated_at
                AFTER UPDATE ON People
                FOR EACH ROW
                BEGIN
                    UPDATE People SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Collection table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Collections_updated_at
                AFTER UPDATE ON Collections
                FOR EACH ROW
                BEGIN
                    UPDATE Collections SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Album table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Albums_updated_at
                AFTER UPDATE ON Albums
                FOR EACH ROW
                BEGIN
                    UPDATE Albums SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Artist table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Artists_updated_at
                AFTER UPDATE ON Artists
                FOR EACH ROW
                BEGIN
                    UPDATE Artists SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Track table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Tracks_updated_at
                AFTER UPDATE ON Tracks
                FOR EACH ROW
                BEGIN
                    UPDATE Tracks SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Playlist table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Playlists_updated_at
                AFTER UPDATE ON Playlists
                FOR EACH ROW
                BEGIN
                    UPDATE Playlists SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Special table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Specials_updated_at
                AFTER UPDATE ON Specials
                FOR EACH ROW
                BEGIN
                    UPDATE Specials SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Notification table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Notifications_updated_at
                AFTER UPDATE ON Notifications
                FOR EACH ROW
                BEGIN
                    UPDATE Notifications SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Message table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Messages_updated_at
                AFTER UPDATE ON Messages
                FOR EACH ROW
                BEGIN
                    UPDATE Messages SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Image table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Images_updated_at
                AFTER UPDATE ON Images
                FOR EACH ROW
                BEGIN
                    UPDATE Images SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // VideoFile table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_VideoFiles_updated_at
                AFTER UPDATE ON VideoFiles
                FOR EACH ROW
                BEGIN
                    UPDATE VideoFiles SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Media table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Medias_updated_at
                AFTER UPDATE ON Medias
                FOR EACH ROW
                BEGIN
                    UPDATE Medias SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // MediaStream table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_MediaStreams_updated_at
                AFTER UPDATE ON MediaStreams
                FOR EACH ROW
                BEGIN
                    UPDATE MediaStreams SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // MediaAttachment table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_MediaAttachments_updated_at
                AFTER UPDATE ON MediaAttachments
                FOR EACH ROW
                BEGIN
                    UPDATE MediaAttachments SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // UserData table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_UserData_updated_at
                AFTER UPDATE ON UserData
                FOR EACH ROW
                BEGIN
                    UPDATE UserData SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Translation table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Translations_updated_at
                AFTER UPDATE ON Translations
                FOR EACH ROW
                BEGIN
                    UPDATE Translations SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // Metadata table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_Metadata_updated_at
                AFTER UPDATE ON Metadata
                FOR EACH ROW
                BEGIN
                    UPDATE Metadata SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // AlternativeTitle table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_AlternativeTitles_updated_at
                AFTER UPDATE ON AlternativeTitles
                FOR EACH ROW
                BEGIN
                    UPDATE AlternativeTitles SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");

            // ReleaseGroup table trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER update_ReleaseGroups_updated_at
                AFTER UPDATE ON ReleaseGroups
                FOR EACH ROW
                BEGIN
                    UPDATE ReleaseGroups SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Users_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_ActivityLogs_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Devices_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Libraries_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Movies_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Tvs_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Seasons_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Episodes_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_People_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Collections_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Albums_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Artists_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Tracks_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Playlists_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Specials_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Notifications_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Messages_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_ColorPalettes_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Images_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_VideoFiles_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Medias_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_MediaStreams_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_MediaAttachments_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Folders_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_UserData_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Translations_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_Metadata_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_AlternativeTitles_updated_at;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_ReleaseGroups_updated_at;");
        }
    }
}