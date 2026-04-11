using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;

namespace NoMercy.Database;

public class MediaContext : DbContext
{
    public MediaContext(DbContextOptions<MediaContext> options)
        : base(options)
    {
        //
    }

    public MediaContext() { }

    [DbFunction("normalize_search", IsBuiltIn = true)]
    public static string NormalizeSearch(string? input) =>
        throw new NotSupportedException("This method is for EF Core query translation only.");

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite(
            $"Data Source={AppFiles.MediaDatabase}; Pooling=True; Foreign Keys=True; Default Timeout=30;",
            o =>
            {
                o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                o.ExecutionStrategy(deps => new SqliteRetryingExecutionStrategy(deps));
            }
        );

        if (Config.IsDev)
            options.EnableSensitiveDataLogging();

        options.AddInterceptors(
            new EntityBaseUpdatedAtInterceptor(),
            new SqliteNormalizeSearchInterceptor(),
            new SqliteConnectionInterceptor()
        );
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<string>().HaveMaxLength(256);

        configurationBuilder.Properties<Ulid>().HaveConversion<UlidToStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(
            typeof(MediaContext).GetMethod(nameof(NormalizeSearch), [typeof(string)])!
        );

        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.Name is "CreatedAt" or "UpdatedAt")
            .ToList()
            .ForEach(p => p.SetDefaultValueSql("CURRENT_TIMESTAMP"));

        // Default to Restrict to prevent accidental cascading deletes across the schema.
        // Relationships that genuinely need cascading (e.g. owned/dependent records) are
        // configured explicitly below with OnDelete(DeleteBehavior.Cascade).
        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .ToList()
            .ForEach(p => p.DeleteBehavior = DeleteBehavior.Restrict);

        modelBuilder.Entity<Cast>().Property(t => t.RoleId).IsRequired(false);

        modelBuilder.Entity<Crew>().Property(t => t.JobId).IsRequired(false);

        // Metadata owns its AudioTrack — delete the track when metadata is removed.
        modelBuilder
            .Entity<Metadata>()
            .HasOne(m => m.AudioTrack)
            .WithOne()
            .HasForeignKey<Metadata>(m => m.AudioTrackId)
            .OnDelete(DeleteBehavior.Cascade);

        // Track owns its Metadata row — delete metadata when the track is removed.
        modelBuilder
            .Entity<Track>()
            .HasOne(t => t.Metadata)
            .WithMany()
            .HasForeignKey(t => t.MetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(string))
            .ToList()
            .ForEach(p =>
            {
                MaxLengthAttribute? maxLengthAttr =
                    p.PropertyInfo?.GetCustomAttribute<MaxLengthAttribute>();
                if (maxLengthAttr is not null)
                    p.SetMaxLength(maxLengthAttr.Length);
            });

        List<IMutableEntityType> entityTypes = modelBuilder
            .Model.GetEntityTypes()
            .Where(t =>
                t.ClrType.IsSubclassOf(typeof(Timestamps)) || t.ClrType == typeof(Timestamps)
            )
            .ToList();

        foreach (IMutableEntityType entityType in entityTypes)
        {
            string? tableName = entityType.GetTableName();
            modelBuilder
                .Entity(entityType.ClrType)
                .ToTable(tb => tb.HasTrigger($"update_{tableName}_updated_at"));
        }

        // Explicit cascade for direct entity → Library FKs. These use ConfigurationSource.Explicit
        // so they cannot be reset by convention re-processing (the mutation loop above only sets
        // ConventionSource and gets overridden when HasTrigger calls re-process those entities).
        modelBuilder
            .Entity<Movie>()
            .HasOne(m => m.Library)
            .WithMany()
            .HasForeignKey(m => m.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<Tv>()
            .HasOne(t => t.Library)
            .WithMany()
            .HasForeignKey(t => t.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<Collection>()
            .HasOne(c => c.Library)
            .WithMany()
            .HasForeignKey(c => c.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<Album>()
            .HasOne(a => a.Library)
            .WithMany()
            .HasForeignKey(a => a.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Content-ownership cascades: when a Movie/VideoFile is deleted, remove its owned records.
        // Use the correct WithMany() collection navigation to match the convention-discovered
        // relationship — otherwise EF Core creates a duplicate FK property (e.g. MovieId1).
        modelBuilder
            .Entity<GenreMovie>()
            .HasOne(gm => gm.Movie)
            .WithMany(m => m.GenreMovies)
            .HasForeignKey(gm => gm.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<VideoFile>()
            .HasOne(v => v.Movie)
            .WithMany(m => m.VideoFiles)
            .HasForeignKey(v => v.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<UserData>()
            .HasOne(u => u.Movie)
            .WithMany(m => m.UserData)
            .HasForeignKey(u => u.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<UserData>()
            .HasOne(u => u.VideoFile)
            .WithMany(v => v.UserData)
            .HasForeignKey(u => u.VideoFileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mutation loop for join table FKs — these are not covered by explicit fluent API above
        // but need Cascade so deleting a Library also removes their join rows.
        // This runs last so it doesn't conflict with the explicit configs above.
        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Library))
            .ToList()
            .ForEach(fk => fk.DeleteBehavior = DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }

    public virtual DbSet<ActivityLog> ActivityLogs { get; init; }
    public virtual DbSet<Cast> Casts { get; init; }
    public virtual DbSet<CertificationMovie> CertificationMovie { get; init; }
    public virtual DbSet<CertificationTv> CertificationTv { get; init; }
    public virtual DbSet<Certification> Certifications { get; init; }
    public virtual DbSet<CollectionLibrary> CollectionLibrary { get; init; }
    public virtual DbSet<CollectionMovie> CollectionMovie { get; init; }
    public virtual DbSet<Collection> Collections { get; init; }
    public virtual DbSet<Country> Countries { get; init; }
    public virtual DbSet<Creator> Creators { get; init; }
    public virtual DbSet<Crew> Crews { get; init; }
    public virtual DbSet<Device> Devices { get; init; }
    public virtual DbSet<EncoderProfileFolder> EncoderProfileFolder { get; init; }
    public virtual DbSet<EncoderProfile> EncoderProfiles { get; init; }
    public virtual DbSet<Episode> Episodes { get; init; }
    public virtual DbSet<FolderLibrary> FolderLibrary { get; init; }
    public virtual DbSet<Folder> Folders { get; init; }
    public virtual DbSet<GenreMovie> GenreMovie { get; init; }
    public virtual DbSet<GenreTv> GenreTv { get; init; }
    public virtual DbSet<Genre> Genres { get; init; }
    public virtual DbSet<GuestStar> GuestStars { get; init; }
    public virtual DbSet<Image> Images { get; init; }
    public virtual DbSet<WatchProvider> WatchProviders { get; init; }
    public virtual DbSet<WatchProviderMedia> WatchProviderMedia { get; init; }
    public virtual DbSet<Network> Networks { get; init; }
    public virtual DbSet<NetworkTv> NetworkTv { get; init; }

    public virtual DbSet<Job> Jobs { get; init; }
    public virtual DbSet<KeywordMovie> KeywordMovie { get; init; }
    public virtual DbSet<KeywordTv> KeywordTv { get; init; }
    public virtual DbSet<Keyword> Keywords { get; init; }
    public virtual DbSet<LanguageLibrary> LanguageLibrary { get; init; }
    public virtual DbSet<Language> Languages { get; init; }
    public virtual DbSet<Library> Libraries { get; init; }
    public virtual DbSet<LibraryMovie> LibraryMovie { get; init; }
    public virtual DbSet<LibraryTv> LibraryTv { get; init; }
    public virtual DbSet<LibraryTrack> LibraryTrack { get; init; }
    public virtual DbSet<LibraryUser> LibraryUser { get; init; }
    public virtual DbSet<CollectionUser> CollectionUser { get; init; }
    public virtual DbSet<MovieUser> MovieUser { get; init; }
    public virtual DbSet<TvUser> TvUser { get; init; }
    public virtual DbSet<SpecialUser> SpecialUser { get; init; }
    public virtual DbSet<MediaAttachment> MediaAttachments { get; init; }
    public virtual DbSet<Media> Medias { get; init; }
    public virtual DbSet<MediaStream> MediaStreams { get; init; }
    public virtual DbSet<Message> Messages { get; init; }
    public virtual DbSet<Metadata> Metadata { get; init; }
    public virtual DbSet<Movie> Movies { get; init; }
    public virtual DbSet<MusicGenreTrack> MusicGenreTrack { get; init; }
    public virtual DbSet<MusicGenre> MusicGenres { get; init; }
    public virtual DbSet<NotificationUser> NotificationUser { get; init; }
    public virtual DbSet<Notification> Notifications { get; init; }
    public virtual DbSet<Person> People { get; init; }
    public virtual DbSet<Playlist> Playlists { get; init; }
    public virtual DbSet<Recommendation> Recommendations { get; init; }
    public virtual DbSet<Role> Roles { get; init; }
    public virtual DbSet<RunningTask> RunningTasks { get; init; }
    public virtual DbSet<Season> Seasons { get; init; }
    public virtual DbSet<Similar> Similar { get; init; }
    public virtual DbSet<SpecialItem> SpecialItems { get; init; }
    public virtual DbSet<Special> Specials { get; init; }
    public virtual DbSet<Translation> Translations { get; init; }
    public virtual DbSet<Tv> Tvs { get; init; }
    public virtual DbSet<UserData> UserData { get; init; }
    public virtual DbSet<User> Users { get; init; }
    public virtual DbSet<VideoFile> VideoFiles { get; init; }
    public virtual DbSet<Company> Companies { get; init; }
    public virtual DbSet<CompanyMovie> CompanyMovie { get; init; }
    public virtual DbSet<CompanyTv> CompanyTv { get; init; }

    public virtual DbSet<AlbumArtist> AlbumArtist { get; init; }
    public virtual DbSet<AlbumLibrary> AlbumLibrary { get; init; }
    public virtual DbSet<AlbumMusicGenre> AlbumMusicGenre { get; init; }
    public virtual DbSet<AlbumTrack> AlbumTrack { get; init; }
    public virtual DbSet<AlbumUser> AlbumUser { get; init; }
    public virtual DbSet<Album> Albums { get; init; }
    public virtual DbSet<AlternativeTitle> AlternativeTitles { get; init; }
    public virtual DbSet<ArtistLibrary> ArtistLibrary { get; init; }
    public virtual DbSet<ArtistMusicGenre> ArtistMusicGenre { get; init; }
    public virtual DbSet<ArtistTrack> ArtistTrack { get; init; }
    public virtual DbSet<ArtistUser> ArtistUser { get; init; }
    public virtual DbSet<Artist> Artists { get; init; }
    public virtual DbSet<MusicPlay> MusicPlays { get; init; }
    public virtual DbSet<PlaylistTrack> PlaylistTrack { get; init; }
    public virtual DbSet<TrackUser> TrackUser { get; init; }
    public virtual DbSet<Track> Tracks { get; init; }
    public virtual DbSet<ReleaseGroup> ReleaseGroups { get; init; }
    public virtual DbSet<AlbumReleaseGroup> AlbumReleaseGroup { get; init; }
    public virtual DbSet<ArtistReleaseGroup> ArtistReleaseGroup { get; init; }
    public virtual DbSet<MusicGenreReleaseGroup> MusicGenreReleaseGroup { get; init; }

    public virtual DbSet<PlaybackPreference> PlaybackPreferences { get; init; }
}
