using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Certifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Iso31661 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Rating = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Meaning = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuration", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Iso31661 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EnglishName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NativeName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Browser = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Os = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Device = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CustomName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    VolumePercent = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncoderProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Container = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Param = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    VideoProfile = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AudioProfile = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SubtitleProfile = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncoderProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Task = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: true),
                    CreditId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keywords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Iso6391 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EnglishName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Libraries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ChapterImages = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtractChapters = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtractChaptersDuring = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AutoRefreshInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: true),
                    PerfectSubtitleMatch = table.Column<bool>(type: "INTEGER", nullable: false),
                    Realtime = table.Column<bool>(type: "INTEGER", nullable: false),
                    SpecialSeasonName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Libraries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaAttachments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaStreams",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaStreams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MusicGenres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicGenres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Adult = table.Column<bool>(type: "INTEGER", nullable: false),
                    AlsoKnownAs = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Biography = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    BirthDay = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeathDay = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Homepage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    KnownForDepartment = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PlaceOfBirth = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Popularity = table.Column<double>(type: "REAL", nullable: false),
                    Profile = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TitleSort = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalIds = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunningTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunningTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Specials",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Backdrop = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Poster = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Logo = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TitleSort = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Manage = table.Column<bool>(type: "INTEGER", nullable: false),
                    Owner = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Allowed = table.Column<bool>(type: "INTEGER", nullable: false),
                    AudioTranscoding = table.Column<bool>(type: "INTEGER", nullable: false),
                    VideoTranscoding = table.Column<bool>(type: "INTEGER", nullable: false),
                    NoTranscoding = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncoderProfileFolder",
                columns: table => new
                {
                    EncoderProfileId = table.Column<string>(type: "TEXT", nullable: false),
                    FolderId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncoderProfileFolder", x => new { x.EncoderProfileId, x.FolderId });
                    table.ForeignKey(
                        name: "FK_EncoderProfileFolder_EncoderProfiles_EncoderProfileId",
                        column: x => x.EncoderProfileId,
                        principalTable: "EncoderProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EncoderProfileFolder_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TrackNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Cover = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Filename = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Quality = table.Column<int>(type: "INTEGER", nullable: true),
                    Lyrics = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    HostFolder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FolderId = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tracks_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Artists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Disambiguation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Cover = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TitleSort = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    HostFolder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: true),
                    FolderId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artists_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Artists_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TitleSort = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Backdrop = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Poster = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Parts = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FolderLibrary",
                columns: table => new
                {
                    FolderId = table.Column<string>(type: "TEXT", nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderLibrary", x => new { x.FolderId, x.LibraryId });
                    table.ForeignKey(
                        name: "FK_FolderLibrary_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FolderLibrary_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LanguageLibrary",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageLibrary", x => new { x.LanguageId, x.LibraryId });
                    table.ForeignKey(
                        name: "FK_LanguageLibrary_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LanguageLibrary_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TitleSort = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: true),
                    Show = table.Column<bool>(type: "INTEGER", nullable: false),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Adult = table.Column<bool>(type: "INTEGER", nullable: false),
                    Backdrop = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Budget = table.Column<int>(type: "INTEGER", nullable: true),
                    Homepage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OriginalTitle = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OriginalLanguage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Popularity = table.Column<double>(type: "REAL", nullable: true),
                    Poster = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Revenue = table.Column<long>(type: "INTEGER", nullable: true),
                    Runtime = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Tagline = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Trailer = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Video = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    VoteAverage = table.Column<double>(type: "REAL", nullable: true),
                    VoteCount = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movies_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Disambiguation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Cover = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseGroups_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tvs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TitleSort = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    HaveEpisodes = table.Column<int>(type: "INTEGER", nullable: true),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Backdrop = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: true),
                    FirstAirDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Homepage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    InProduction = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastEpisodeToAir = table.Column<int>(type: "INTEGER", nullable: true),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NextEpisodeToAir = table.Column<int>(type: "INTEGER", nullable: true),
                    NumberOfEpisodes = table.Column<int>(type: "INTEGER", nullable: false),
                    NumberOfSeasons = table.Column<int>(type: "INTEGER", nullable: true),
                    OriginCountry = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OriginalLanguage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Popularity = table.Column<double>(type: "REAL", nullable: true),
                    Poster = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SpokenLanguages = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Tagline = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Trailer = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    VoteAverage = table.Column<double>(type: "REAL", nullable: true),
                    VoteCount = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tvs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tvs_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriorityProvider",
                columns: table => new
                {
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProviderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorityProvider", x => new { x.Priority, x.Country, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_PriorityProvider_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryUser",
                columns: table => new
                {
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryUser", x => new { x.LibraryId, x.UserId });
                    table.ForeignKey(
                        name: "FK_LibraryUser_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationUser",
                columns: table => new
                {
                    NotificationId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationUser", x => new { x.NotificationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_NotificationUser_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Cover = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Filename = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpecialUser",
                columns: table => new
                {
                    SpecialId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialUser", x => new { x.SpecialId, x.UserId });
                    table.ForeignKey(
                        name: "FK_SpecialUser_Specials_SpecialId",
                        column: x => x.SpecialId,
                        principalTable: "Specials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecialUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryTrack",
                columns: table => new
                {
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryTrack", x => new { x.LibraryId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_LibraryTrack_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryTrack_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Metadata",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Filename = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    HostFolder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FolderSize = table.Column<long>(type: "INTEGER", nullable: false),
                    AudioTrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Previews = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Fonts = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FontsFile = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ChaptersFile = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Chapters = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Video = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Audio = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Subtitles = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Metadata_Tracks_AudioTrackId",
                        column: x => x.AudioTrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicGenreTrack",
                columns: table => new
                {
                    GenreId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicGenreTrack", x => new { x.GenreId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_MusicGenreTrack_MusicGenres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "MusicGenres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MusicGenreTrack_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicPlays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicPlays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicPlays_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MusicPlays_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackUser",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackUser", x => new { x.TrackId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TrackUser_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrackUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtistLibrary",
                columns: table => new
                {
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistLibrary", x => new { x.ArtistId, x.LibraryId });
                    table.ForeignKey(
                        name: "FK_ArtistLibrary_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistLibrary_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtistMusicGenre",
                columns: table => new
                {
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MusicGenreId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistMusicGenre", x => new { x.ArtistId, x.MusicGenreId });
                    table.ForeignKey(
                        name: "FK_ArtistMusicGenre_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistMusicGenre_MusicGenres_MusicGenreId",
                        column: x => x.MusicGenreId,
                        principalTable: "MusicGenres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtistTrack",
                columns: table => new
                {
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistTrack", x => new { x.ArtistId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_ArtistTrack_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistTrack_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtistUser",
                columns: table => new
                {
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistUser", x => new { x.ArtistId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ArtistUser_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionLibrary",
                columns: table => new
                {
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionLibrary", x => new { x.CollectionId, x.LibraryId });
                    table.ForeignKey(
                        name: "FK_CollectionLibrary_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionLibrary_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionUser",
                columns: table => new
                {
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionUser", x => new { x.CollectionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CollectionUser_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificationMovie",
                columns: table => new
                {
                    CertificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationMovie", x => new { x.CertificationId, x.MovieId });
                    table.ForeignKey(
                        name: "FK_CertificationMovie_Certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "Certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CertificationMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionMovie",
                columns: table => new
                {
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionMovie", x => new { x.CollectionId, x.MovieId });
                    table.ForeignKey(
                        name: "FK_CollectionMovie_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GenreMovie",
                columns: table => new
                {
                    GenreId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenreMovie", x => new { x.GenreId, x.MovieId });
                    table.ForeignKey(
                        name: "FK_GenreMovie_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GenreMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KeywordMovie",
                columns: table => new
                {
                    KeywordId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordMovie", x => new { x.KeywordId, x.MovieId });
                    table.ForeignKey(
                        name: "FK_KeywordMovie_Keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "Keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KeywordMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryMovie",
                columns: table => new
                {
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryMovie", x => new { x.LibraryId, x.MovieId });
                    table.ForeignKey(
                        name: "FK_LibraryMovie_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryMovie_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovieUser",
                columns: table => new
                {
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieUser", x => new { x.MovieId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MovieUser_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovieUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtistReleaseGroup",
                columns: table => new
                {
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReleaseGroupId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistReleaseGroup", x => new { x.ArtistId, x.ReleaseGroupId });
                    table.ForeignKey(
                        name: "FK_ArtistReleaseGroup_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistReleaseGroup_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicGenreReleaseGroup",
                columns: table => new
                {
                    GenreId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReleaseGroupId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicGenreReleaseGroup", x => new { x.GenreId, x.ReleaseGroupId });
                    table.ForeignKey(
                        name: "FK_MusicGenreReleaseGroup_MusicGenres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "MusicGenres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MusicGenreReleaseGroup_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlternativeTitles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Iso31661 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlternativeTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlternativeTitles_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlternativeTitles_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificationTv",
                columns: table => new
                {
                    CertificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationTv", x => new { x.CertificationId, x.TvId });
                    table.ForeignKey(
                        name: "FK_CertificationTv_Certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "Certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CertificationTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Creators",
                columns: table => new
                {
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creators", x => new { x.PersonId, x.TvId });
                    table.ForeignKey(
                        name: "FK_Creators_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Creators_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GenreTv",
                columns: table => new
                {
                    GenreId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenreTv", x => new { x.GenreId, x.TvId });
                    table.ForeignKey(
                        name: "FK_GenreTv_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GenreTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KeywordTv",
                columns: table => new
                {
                    KeywordId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordTv", x => new { x.KeywordId, x.TvId });
                    table.ForeignKey(
                        name: "FK_KeywordTv_Keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "Keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KeywordTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryTv",
                columns: table => new
                {
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryTv", x => new { x.LibraryId, x.TvId });
                    table.ForeignKey(
                        name: "FK_LibraryTv_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryTv_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaybackPreferences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    SpecialId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Video = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Audio = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Subtitles = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackPreferences_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybackPreferences_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybackPreferences_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybackPreferences_Specials_SpecialId",
                        column: x => x.SpecialId,
                        principalTable: "Specials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybackPreferences_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybackPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Backdrop = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Poster = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TitleSort = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvFromId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvToId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieFromId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieToId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recommendations_Movies_MovieFromId",
                        column: x => x.MovieFromId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recommendations_Movies_MovieToId",
                        column: x => x.MovieToId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recommendations_Tvs_TvFromId",
                        column: x => x.TvFromId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recommendations_Tvs_TvToId",
                        column: x => x.TvToId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AirDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Poster = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Seasons_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Similar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Backdrop = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Poster = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TitleSort = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    TvFromId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvToId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieFromId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieToId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Similar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Similar_Movies_MovieFromId",
                        column: x => x.MovieFromId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Similar_Movies_MovieToId",
                        column: x => x.MovieToId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Similar_Tvs_TvFromId",
                        column: x => x.TvFromId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Similar_Tvs_TvToId",
                        column: x => x.TvToId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TvUser",
                columns: table => new
                {
                    TvId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvUser", x => new { x.TvId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TvUser_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TvUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistTrack",
                columns: table => new
                {
                    PlaylistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistTrack", x => new { x.PlaylistId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_PlaylistTrack_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistTrack_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Disambiguation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Cover = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Tracks = table.Column<int>(type: "INTEGER", nullable: false),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    HostFolder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false),
                    FolderId = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Albums_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Albums_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Albums_Metadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "Metadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Episodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AirDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ProductionCode = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Still = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    VoteAverage = table.Column<float>(type: "REAL", nullable: true),
                    VoteCount = table.Column<int>(type: "INTEGER", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Episodes_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Episodes_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlbumArtist",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumArtist", x => new { x.AlbumId, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_AlbumArtist_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumArtist_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlbumLibrary",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumLibrary", x => new { x.AlbumId, x.LibraryId });
                    table.ForeignKey(
                        name: "FK_AlbumLibrary_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumLibrary_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlbumMusicGenre",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MusicGenreId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumMusicGenre", x => new { x.AlbumId, x.MusicGenreId });
                    table.ForeignKey(
                        name: "FK_AlbumMusicGenre_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumMusicGenre_MusicGenres_MusicGenreId",
                        column: x => x.MusicGenreId,
                        principalTable: "MusicGenres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlbumReleaseGroup",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReleaseGroupId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumReleaseGroup", x => new { x.AlbumId, x.ReleaseGroupId });
                    table.ForeignKey(
                        name: "FK_AlbumReleaseGroup_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumReleaseGroup_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlbumTrack",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumTrack", x => new { x.AlbumId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_AlbumTrack_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumTrack_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlbumUser",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumUser", x => new { x.AlbumId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AlbumUser_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumUser_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Crews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreditId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crews_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Crews_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Crews_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Crews_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Crews_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Crews_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuestStars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreditId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestStars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestStars_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestStars_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpecialItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    SpecialId = table.Column<string>(type: "TEXT", nullable: false),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecialItems_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecialItems_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecialItems_Specials_SpecialId",
                        column: x => x.SpecialId,
                        principalTable: "Specials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Translations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Iso31661 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Iso6391 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EnglishName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Overview = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Homepage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Biography = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReleaseGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GenreId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Translations_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Translations_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Filename = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    HostFolder = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Languages = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Quality = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Share = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Subtitles = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Chapters = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    MetadataId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Track = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoFiles_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoFiles_Metadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "Metadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoFiles_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Character = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: true),
                    CreditId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    GuestStarId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_GuestStars_GuestStarId",
                        column: x => x.GuestStarId,
                        principalTable: "GuestStars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Medias",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Iso6391 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Site = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Size = table.Column<int>(type: "INTEGER", nullable: false),
                    Src = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoFileId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Medias_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_VideoFiles_VideoFileId",
                        column: x => x.VideoFileId,
                        principalTable: "VideoFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserData",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    LastPlayedDate = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Audio = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Subtitle = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SubtitleType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Time = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    SpecialId = table.Column<string>(type: "TEXT", nullable: true),
                    VideoFileId = table.Column<string>(type: "TEXT", nullable: false),
                    SpecialItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserData_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserData_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserData_SpecialItems_SpecialItemId",
                        column: x => x.SpecialItemId,
                        principalTable: "SpecialItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserData_Specials_SpecialId",
                        column: x => x.SpecialId,
                        principalTable: "Specials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserData_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserData_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserData_VideoFiles_VideoFileId",
                        column: x => x.VideoFileId,
                        principalTable: "VideoFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Casts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreditId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Casts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Casts_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Casts_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Casts_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Casts_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Casts_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Casts_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AspectRatio = table.Column<double>(type: "REAL", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    Iso6391 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Site = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Size = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    VoteAverage = table.Column<double>(type: "REAL", nullable: true),
                    VoteCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    CastCreditId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CastId = table.Column<int>(type: "INTEGER", nullable: true),
                    CrewCreditId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CrewId = table.Column<int>(type: "INTEGER", nullable: true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TvId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ColorPalette = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Casts_CastId",
                        column: x => x.CastId,
                        principalTable: "Casts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_Tvs_TvId",
                        column: x => x.TvId,
                        principalTable: "Tvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_DeviceId",
                table: "ActivityLogs",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId",
                table: "ActivityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumArtist_AlbumId",
                table: "AlbumArtist",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumArtist_ArtistId",
                table: "AlbumArtist",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumLibrary_AlbumId",
                table: "AlbumLibrary",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumLibrary_LibraryId",
                table: "AlbumLibrary",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumMusicGenre_AlbumId",
                table: "AlbumMusicGenre",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumMusicGenre_MusicGenreId",
                table: "AlbumMusicGenre",
                column: "MusicGenreId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumReleaseGroup_AlbumId",
                table: "AlbumReleaseGroup",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumReleaseGroup_ReleaseGroupId",
                table: "AlbumReleaseGroup",
                column: "ReleaseGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_FolderId",
                table: "Albums",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_LibraryId",
                table: "Albums",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_MetadataId",
                table: "Albums",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_Name",
                table: "Albums",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_Year",
                table: "Albums",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumTrack_AlbumId",
                table: "AlbumTrack",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumTrack_TrackId",
                table: "AlbumTrack",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumUser_AlbumId",
                table: "AlbumUser",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumUser_UserId",
                table: "AlbumUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AlternativeTitles_MovieId",
                table: "AlternativeTitles",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_AlternativeTitles_Title_MovieId",
                table: "AlternativeTitles",
                columns: new[] { "Title", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlternativeTitles_Title_TvId",
                table: "AlternativeTitles",
                columns: new[] { "Title", "TvId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlternativeTitles_TvId",
                table: "AlternativeTitles",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistLibrary_ArtistId",
                table: "ArtistLibrary",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistLibrary_LibraryId",
                table: "ArtistLibrary",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistMusicGenre_ArtistId",
                table: "ArtistMusicGenre",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistMusicGenre_MusicGenreId",
                table: "ArtistMusicGenre",
                column: "MusicGenreId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistReleaseGroup_ArtistId",
                table: "ArtistReleaseGroup",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistReleaseGroup_ReleaseGroupId",
                table: "ArtistReleaseGroup",
                column: "ReleaseGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_Country",
                table: "Artists",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_FolderId",
                table: "Artists",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_LibraryId",
                table: "Artists",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_Name",
                table: "Artists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_Year",
                table: "Artists",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistTrack_ArtistId",
                table: "ArtistTrack",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistTrack_TrackId",
                table: "ArtistTrack",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistUser_ArtistId",
                table: "ArtistUser",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistUser_UserId",
                table: "ArtistUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_CreditId",
                table: "Casts",
                column: "CreditId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_CreditId_EpisodeId_RoleId",
                table: "Casts",
                columns: new[] { "CreditId", "EpisodeId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Casts_CreditId_MovieId_RoleId",
                table: "Casts",
                columns: new[] { "CreditId", "MovieId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Casts_CreditId_SeasonId_RoleId",
                table: "Casts",
                columns: new[] { "CreditId", "SeasonId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Casts_CreditId_TvId_RoleId",
                table: "Casts",
                columns: new[] { "CreditId", "TvId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Casts_EpisodeId",
                table: "Casts",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_MovieId",
                table: "Casts",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_PersonId",
                table: "Casts",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_RoleId",
                table: "Casts",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_SeasonId",
                table: "Casts",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Casts_TvId",
                table: "Casts",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationMovie_CertificationId",
                table: "CertificationMovie",
                column: "CertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationMovie_MovieId",
                table: "CertificationMovie",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Certifications_Iso31661_Rating",
                table: "Certifications",
                columns: new[] { "Iso31661", "Rating" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certifications_Order",
                table: "Certifications",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_Certifications_Rating",
                table: "Certifications",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationTv_CertificationId",
                table: "CertificationTv",
                column: "CertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationTv_TvId",
                table: "CertificationTv",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionLibrary_CollectionId",
                table: "CollectionLibrary",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionLibrary_LibraryId",
                table: "CollectionLibrary",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionMovie_CollectionId",
                table: "CollectionMovie",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionMovie_MovieId",
                table: "CollectionMovie",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_LibraryId",
                table: "Collections",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Title",
                table: "Collections",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_TitleSort",
                table: "Collections",
                column: "TitleSort");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionUser_CollectionId",
                table: "CollectionUser",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionUser_UserId",
                table: "CollectionUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Configuration_Key",
                table: "Configuration",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_EnglishName",
                table: "Countries",
                column: "EnglishName");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Iso31661",
                table: "Countries",
                column: "Iso31661",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_NativeName",
                table: "Countries",
                column: "NativeName");

            migrationBuilder.CreateIndex(
                name: "IX_Creators_TvId",
                table: "Creators",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_CreditId",
                table: "Crews",
                column: "CreditId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_CreditId_EpisodeId_JobId",
                table: "Crews",
                columns: new[] { "CreditId", "EpisodeId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Crews_CreditId_MovieId_JobId",
                table: "Crews",
                columns: new[] { "CreditId", "MovieId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Crews_CreditId_SeasonId_JobId",
                table: "Crews",
                columns: new[] { "CreditId", "SeasonId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Crews_CreditId_TvId_JobId",
                table: "Crews",
                columns: new[] { "CreditId", "TvId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Crews_EpisodeId",
                table: "Crews",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_JobId",
                table: "Crews",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_MovieId",
                table: "Crews",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_PersonId",
                table: "Crews",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_SeasonId",
                table: "Crews",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Crews_TvId",
                table: "Crews",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceId",
                table: "Devices",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncoderProfileFolder_EncoderProfileId",
                table: "EncoderProfileFolder",
                column: "EncoderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_EncoderProfileFolder_FolderId",
                table: "EncoderProfileFolder",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_AirDate",
                table: "Episodes",
                column: "AirDate");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_EpisodeNumber",
                table: "Episodes",
                column: "EpisodeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_ImdbId",
                table: "Episodes",
                column: "ImdbId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeasonId",
                table: "Episodes",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeasonNumber",
                table: "Episodes",
                column: "SeasonNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_Title",
                table: "Episodes",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_TvdbId",
                table: "Episodes",
                column: "TvdbId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_TvId",
                table: "Episodes",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderLibrary_FolderId",
                table: "FolderLibrary",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderLibrary_LibraryId",
                table: "FolderLibrary",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_Path",
                table: "Folders",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenreMovie_GenreId",
                table: "GenreMovie",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_GenreMovie_MovieId",
                table: "GenreMovie",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                table: "Genres",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GenreTv_GenreId",
                table: "GenreTv",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_GenreTv_TvId",
                table: "GenreTv",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestStars_CreditId",
                table: "GuestStars",
                column: "CreditId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestStars_CreditId_EpisodeId",
                table: "GuestStars",
                columns: new[] { "CreditId", "EpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestStars_EpisodeId",
                table: "GuestStars",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestStars_PersonId",
                table: "GuestStars",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_AlbumId",
                table: "Images",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_ArtistId",
                table: "Images",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CastCreditId",
                table: "Images",
                column: "CastCreditId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CastId",
                table: "Images",
                column: "CastId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CollectionId",
                table: "Images",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CrewCreditId",
                table: "Images",
                column: "CrewCreditId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CrewId",
                table: "Images",
                column: "CrewId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_EpisodeId",
                table: "Images",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath",
                table: "Images",
                column: "FilePath");

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_AlbumId",
                table: "Images",
                columns: new[] { "FilePath", "AlbumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_ArtistId",
                table: "Images",
                columns: new[] { "FilePath", "ArtistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_CastCreditId",
                table: "Images",
                columns: new[] { "FilePath", "CastCreditId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_CollectionId",
                table: "Images",
                columns: new[] { "FilePath", "CollectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_CrewCreditId",
                table: "Images",
                columns: new[] { "FilePath", "CrewCreditId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_EpisodeId",
                table: "Images",
                columns: new[] { "FilePath", "EpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_MovieId",
                table: "Images",
                columns: new[] { "FilePath", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_PersonId",
                table: "Images",
                columns: new[] { "FilePath", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_SeasonId",
                table: "Images",
                columns: new[] { "FilePath", "SeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_TrackId",
                table: "Images",
                columns: new[] { "FilePath", "TrackId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_FilePath_TvId",
                table: "Images",
                columns: new[] { "FilePath", "TvId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_MovieId",
                table: "Images",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_PersonId",
                table: "Images",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_SeasonId",
                table: "Images",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_TrackId",
                table: "Images",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_TvId",
                table: "Images",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CreditId",
                table: "Jobs",
                column: "CreditId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KeywordMovie_KeywordId",
                table: "KeywordMovie",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_KeywordMovie_MovieId",
                table: "KeywordMovie",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_Name",
                table: "Keywords",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTv_KeywordId",
                table: "KeywordTv",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTv_TvId",
                table: "KeywordTv",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageLibrary_LanguageId",
                table: "LanguageLibrary",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageLibrary_LibraryId",
                table: "LanguageLibrary",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_EnglishName",
                table: "Languages",
                column: "EnglishName");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Iso6391",
                table: "Languages",
                column: "Iso6391",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Name",
                table: "Languages",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_Id",
                table: "Libraries",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_Order",
                table: "Libraries",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_Title",
                table: "Libraries",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_Type",
                table: "Libraries",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryMovie_LibraryId",
                table: "LibraryMovie",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryMovie_MovieId",
                table: "LibraryMovie",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryTrack_LibraryId",
                table: "LibraryTrack",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryTrack_TrackId",
                table: "LibraryTrack",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryTv_LibraryId",
                table: "LibraryTv",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryTv_TvId",
                table: "LibraryTv",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryUser_LibraryId",
                table: "LibraryUser",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryUser_UserId",
                table: "LibraryUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_EpisodeId_Src",
                table: "Medias",
                columns: new[] { "EpisodeId", "Src" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_MovieId_Src",
                table: "Medias",
                columns: new[] { "MovieId", "Src" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Name",
                table: "Medias",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_PersonId_Src",
                table: "Medias",
                columns: new[] { "PersonId", "Src" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SeasonId_Src",
                table: "Medias",
                columns: new[] { "SeasonId", "Src" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Site",
                table: "Medias",
                column: "Site");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_TvId_Src",
                table: "Medias",
                columns: new[] { "TvId", "Src" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Type",
                table: "Medias",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_VideoFileId_Src",
                table: "Medias",
                columns: new[] { "VideoFileId", "Src" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metadata_AudioTrackId",
                table: "Metadata",
                column: "AudioTrackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metadata_Filename_HostFolder",
                table: "Metadata",
                columns: new[] { "Filename", "HostFolder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metadata_Type",
                table: "Metadata",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_ImdbId",
                table: "Movies",
                column: "ImdbId");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_LibraryId",
                table: "Movies",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_ReleaseDate",
                table: "Movies",
                column: "ReleaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Title",
                table: "Movies",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_TitleSort",
                table: "Movies",
                column: "TitleSort");

            migrationBuilder.CreateIndex(
                name: "IX_MovieUser_MovieId",
                table: "MovieUser",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieUser_UserId",
                table: "MovieUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicGenreReleaseGroup_GenreId",
                table: "MusicGenreReleaseGroup",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicGenreReleaseGroup_ReleaseGroupId",
                table: "MusicGenreReleaseGroup",
                column: "ReleaseGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicGenres_Name",
                table: "MusicGenres",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MusicGenreTrack_GenreId",
                table: "MusicGenreTrack",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicGenreTrack_TrackId",
                table: "MusicGenreTrack",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicPlays_TrackId",
                table: "MusicPlays",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicPlays_UserId",
                table: "MusicPlays",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationUser_NotificationId",
                table: "NotificationUser",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationUser_UserId",
                table: "NotificationUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_People_BirthDay",
                table: "People",
                column: "BirthDay");

            migrationBuilder.CreateIndex(
                name: "IX_People_ImdbId",
                table: "People",
                column: "ImdbId");

            migrationBuilder.CreateIndex(
                name: "IX_People_Name",
                table: "People",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_People_Popularity",
                table: "People",
                column: "Popularity");

            migrationBuilder.CreateIndex(
                name: "IX_People_TitleSort",
                table: "People",
                column: "TitleSort");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_CollectionId",
                table: "PlaybackPreferences",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_LibraryId",
                table: "PlaybackPreferences",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_MovieId",
                table: "PlaybackPreferences",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_SpecialId",
                table: "PlaybackPreferences",
                column: "SpecialId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_TvId",
                table: "PlaybackPreferences",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_UserId_LibraryId",
                table: "PlaybackPreferences",
                columns: new[] { "UserId", "LibraryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_UserId_MovieId",
                table: "PlaybackPreferences",
                columns: new[] { "UserId", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPreferences_UserId_TvId",
                table: "PlaybackPreferences",
                columns: new[] { "UserId", "TvId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_UserId",
                table: "Playlists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistTrack_PlaylistId",
                table: "PlaylistTrack",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistTrack_TrackId",
                table: "PlaylistTrack",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_PriorityProvider_Country",
                table: "PriorityProvider",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_PriorityProvider_Priority",
                table: "PriorityProvider",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_PriorityProvider_ProviderId",
                table: "PriorityProvider",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_MediaId_MovieFromId",
                table: "Recommendations",
                columns: new[] { "MediaId", "MovieFromId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_MediaId_TvFromId",
                table: "Recommendations",
                columns: new[] { "MediaId", "TvFromId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_MovieFromId",
                table: "Recommendations",
                column: "MovieFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_MovieToId",
                table: "Recommendations",
                column: "MovieToId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_TvFromId",
                table: "Recommendations",
                column: "TvFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_TvToId",
                table: "Recommendations",
                column: "TvToId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseGroups_LibraryId",
                table: "ReleaseGroups",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CreditId",
                table: "Roles",
                column: "CreditId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_GuestStarId",
                table: "Roles",
                column: "GuestStarId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_AirDate",
                table: "Seasons",
                column: "AirDate");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_MovieId",
                table: "Seasons",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_SeasonNumber",
                table: "Seasons",
                column: "SeasonNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_Title",
                table: "Seasons",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_TvId",
                table: "Seasons",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_Similar_MediaId_MovieFromId",
                table: "Similar",
                columns: new[] { "MediaId", "MovieFromId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Similar_MediaId_TvFromId",
                table: "Similar",
                columns: new[] { "MediaId", "TvFromId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Similar_MovieFromId",
                table: "Similar",
                column: "MovieFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Similar_MovieToId",
                table: "Similar",
                column: "MovieToId");

            migrationBuilder.CreateIndex(
                name: "IX_Similar_Title",
                table: "Similar",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Similar_TitleSort",
                table: "Similar",
                column: "TitleSort");

            migrationBuilder.CreateIndex(
                name: "IX_Similar_TvFromId",
                table: "Similar",
                column: "TvFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Similar_TvToId",
                table: "Similar",
                column: "TvToId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialItems_EpisodeId",
                table: "SpecialItems",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialItems_MovieId",
                table: "SpecialItems",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialItems_SpecialId_EpisodeId",
                table: "SpecialItems",
                columns: new[] { "SpecialId", "EpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialItems_SpecialId_MovieId",
                table: "SpecialItems",
                columns: new[] { "SpecialId", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialUser_UserId",
                table: "SpecialUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_DiscNumber",
                table: "Tracks",
                column: "DiscNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Filename",
                table: "Tracks",
                column: "Filename");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Folder",
                table: "Tracks",
                column: "Folder");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_FolderId",
                table: "Tracks",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Name",
                table: "Tracks",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_TrackNumber",
                table: "Tracks",
                column: "TrackNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TrackUser_TrackId",
                table: "TrackUser",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackUser_UserId",
                table: "TrackUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_AlbumId",
                table: "Translations",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_AlbumId_Iso31661",
                table: "Translations",
                columns: new[] { "AlbumId", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_ArtistId",
                table: "Translations",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_ArtistId_Iso31661",
                table: "Translations",
                columns: new[] { "ArtistId", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_CollectionId",
                table: "Translations",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_CollectionId_Iso6391_Iso31661",
                table: "Translations",
                columns: new[] { "CollectionId", "Iso6391", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_EpisodeId",
                table: "Translations",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_EpisodeId_Iso6391_Iso31661",
                table: "Translations",
                columns: new[] { "EpisodeId", "Iso6391", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_GenreId",
                table: "Translations",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_GenreId_Iso6391",
                table: "Translations",
                columns: new[] { "GenreId", "Iso6391" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_MovieId",
                table: "Translations",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_MovieId_Iso6391_Iso31661",
                table: "Translations",
                columns: new[] { "MovieId", "Iso6391", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_PersonId",
                table: "Translations",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_PersonId_Iso6391_Iso31661",
                table: "Translations",
                columns: new[] { "PersonId", "Iso6391", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_ReleaseGroupId",
                table: "Translations",
                column: "ReleaseGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_ReleaseGroupId_Iso31661",
                table: "Translations",
                columns: new[] { "ReleaseGroupId", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_SeasonId",
                table: "Translations",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_SeasonId_Iso6391_Iso31661",
                table: "Translations",
                columns: new[] { "SeasonId", "Iso6391", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Translations_TvId",
                table: "Translations",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_TvId_Iso6391_Iso31661",
                table: "Translations",
                columns: new[] { "TvId", "Iso6391", "Iso31661" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tvs_FirstAirDate",
                table: "Tvs",
                column: "FirstAirDate");

            migrationBuilder.CreateIndex(
                name: "IX_Tvs_ImdbId",
                table: "Tvs",
                column: "ImdbId");

            migrationBuilder.CreateIndex(
                name: "IX_Tvs_LibraryId",
                table: "Tvs",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tvs_Title",
                table: "Tvs",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Tvs_TitleSort",
                table: "Tvs",
                column: "TitleSort");

            migrationBuilder.CreateIndex(
                name: "IX_Tvs_TvdbId",
                table: "Tvs",
                column: "TvdbId");

            migrationBuilder.CreateIndex(
                name: "IX_TvUser_TvId",
                table: "TvUser",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_TvUser_UserId",
                table: "TvUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_CollectionId",
                table: "UserData",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_MovieId",
                table: "UserData",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_SpecialId",
                table: "UserData",
                column: "SpecialId");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_SpecialItemId",
                table: "UserData",
                column: "SpecialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_TvId",
                table: "UserData",
                column: "TvId");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId",
                table: "UserData",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_VideoFileId",
                table: "UserData",
                column: "VideoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_VideoFileId_UserId_CollectionId",
                table: "UserData",
                columns: new[] { "VideoFileId", "UserId", "CollectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserData_VideoFileId_UserId_MovieId",
                table: "UserData",
                columns: new[] { "VideoFileId", "UserId", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserData_VideoFileId_UserId_SpecialId",
                table: "UserData",
                columns: new[] { "VideoFileId", "UserId", "SpecialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserData_VideoFileId_UserId_TvId",
                table: "UserData",
                columns: new[] { "VideoFileId", "UserId", "TvId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Allowed",
                table: "Users",
                column: "Allowed");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Manage",
                table: "Users",
                column: "Manage");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Name",
                table: "Users",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Owner",
                table: "Users",
                column: "Owner");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_Duration",
                table: "VideoFiles",
                column: "Duration");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_EpisodeId",
                table: "VideoFiles",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_Filename",
                table: "VideoFiles",
                column: "Filename",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_Folder",
                table: "VideoFiles",
                column: "Folder");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_MetadataId",
                table: "VideoFiles",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_MovieId",
                table: "VideoFiles",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_Quality",
                table: "VideoFiles",
                column: "Quality");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "AlbumArtist");

            migrationBuilder.DropTable(
                name: "AlbumLibrary");

            migrationBuilder.DropTable(
                name: "AlbumMusicGenre");

            migrationBuilder.DropTable(
                name: "AlbumReleaseGroup");

            migrationBuilder.DropTable(
                name: "AlbumTrack");

            migrationBuilder.DropTable(
                name: "AlbumUser");

            migrationBuilder.DropTable(
                name: "AlternativeTitles");

            migrationBuilder.DropTable(
                name: "ArtistLibrary");

            migrationBuilder.DropTable(
                name: "ArtistMusicGenre");

            migrationBuilder.DropTable(
                name: "ArtistReleaseGroup");

            migrationBuilder.DropTable(
                name: "ArtistTrack");

            migrationBuilder.DropTable(
                name: "ArtistUser");

            migrationBuilder.DropTable(
                name: "CertificationMovie");

            migrationBuilder.DropTable(
                name: "CertificationTv");

            migrationBuilder.DropTable(
                name: "CollectionLibrary");

            migrationBuilder.DropTable(
                name: "CollectionMovie");

            migrationBuilder.DropTable(
                name: "CollectionUser");

            migrationBuilder.DropTable(
                name: "Configuration");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "Creators");

            migrationBuilder.DropTable(
                name: "EncoderProfileFolder");

            migrationBuilder.DropTable(
                name: "FolderLibrary");

            migrationBuilder.DropTable(
                name: "GenreMovie");

            migrationBuilder.DropTable(
                name: "GenreTv");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "KeywordMovie");

            migrationBuilder.DropTable(
                name: "KeywordTv");

            migrationBuilder.DropTable(
                name: "LanguageLibrary");

            migrationBuilder.DropTable(
                name: "LibraryMovie");

            migrationBuilder.DropTable(
                name: "LibraryTrack");

            migrationBuilder.DropTable(
                name: "LibraryTv");

            migrationBuilder.DropTable(
                name: "LibraryUser");

            migrationBuilder.DropTable(
                name: "MediaAttachments");

            migrationBuilder.DropTable(
                name: "Medias");

            migrationBuilder.DropTable(
                name: "MediaStreams");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "MovieUser");

            migrationBuilder.DropTable(
                name: "MusicGenreReleaseGroup");

            migrationBuilder.DropTable(
                name: "MusicGenreTrack");

            migrationBuilder.DropTable(
                name: "MusicPlays");

            migrationBuilder.DropTable(
                name: "NotificationUser");

            migrationBuilder.DropTable(
                name: "PlaybackPreferences");

            migrationBuilder.DropTable(
                name: "PlaylistTrack");

            migrationBuilder.DropTable(
                name: "PriorityProvider");

            migrationBuilder.DropTable(
                name: "Recommendations");

            migrationBuilder.DropTable(
                name: "RunningTasks");

            migrationBuilder.DropTable(
                name: "Similar");

            migrationBuilder.DropTable(
                name: "SpecialUser");

            migrationBuilder.DropTable(
                name: "TrackUser");

            migrationBuilder.DropTable(
                name: "Translations");

            migrationBuilder.DropTable(
                name: "TvUser");

            migrationBuilder.DropTable(
                name: "UserData");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Certifications");

            migrationBuilder.DropTable(
                name: "EncoderProfiles");

            migrationBuilder.DropTable(
                name: "Casts");

            migrationBuilder.DropTable(
                name: "Crews");

            migrationBuilder.DropTable(
                name: "Keywords");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "MusicGenres");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropTable(
                name: "Providers");

            migrationBuilder.DropTable(
                name: "Albums");

            migrationBuilder.DropTable(
                name: "Artists");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "ReleaseGroups");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "SpecialItems");

            migrationBuilder.DropTable(
                name: "VideoFiles");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Specials");

            migrationBuilder.DropTable(
                name: "Metadata");

            migrationBuilder.DropTable(
                name: "GuestStars");

            migrationBuilder.DropTable(
                name: "Tracks");

            migrationBuilder.DropTable(
                name: "Episodes");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "Movies");

            migrationBuilder.DropTable(
                name: "Tvs");

            migrationBuilder.DropTable(
                name: "Libraries");
        }
    }
}
