// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.MediaProcessing.ReleaseGroups;
using NoMercy.MediaProcessing.Releases;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddReleaseOnlyJob : AbstractReleaseJob
{
    public override string QueueName => "queue";
    public override int Priority => 6;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        ReleaseGroupRepository releaseGroupRepository = new(context);
        ReleaseGroupManager releaseGroupManager = new(releaseGroupRepository);

        MusicGenreRepository musicGenreRepository = new(context);

        ReleaseRepository releaseRepository = new(context);
        ReleaseManager releaseManager = new(releaseRepository, musicGenreRepository, jobDispatcher);

        ArtistRepository artistRepository = new(context);
        ArtistManager artistManager = new(artistRepository, musicGenreRepository, jobDispatcher);

        RecordingRepository recordingRepository = new(context);
        RecordingManager recordingManager = new(recordingRepository, musicGenreRepository);

        await using LibraryRepository libraryRepository = new(context);
        Library? albumLibrary = await libraryRepository.GetLibraryByIdWithFolders(LibraryId);

        if (albumLibrary is null)
        {
            Logger.App($"Library not found: {LibraryId}", LogEventLevel.Error);
            return;
        }

        (MusicBrainzReleaseAppends? releaseAppends, CoverArtImageManagerManager.CoverPalette? coverPalette)
            = await releaseManager.Add(Id, albumLibrary, BaseFolder, MediaFolder);

        if (releaseAppends is null || string.IsNullOrEmpty(releaseAppends.Title))
        {
            Logger.App($"Release not found: {Id}", LogEventLevel.Warning);
            await Task.CompletedTask;
            return;
        }

        Logger.App($"Processing release: {releaseAppends.Title} with id: {releaseAppends.Id}", LogEventLevel.Debug);

        await releaseGroupManager.Store(releaseAppends.MusicBrainzReleaseGroup, LibraryId, coverPalette);

        foreach (ReleaseArtistCredit? artist in releaseAppends.ArtistCredit)
        {
            await artistManager.Store(artist, albumLibrary, BaseFolder, MediaFolder, releaseAppends);

            jobDispatcher.DispatchJob<MusicDescriptionJob>(artist.MusicBrainzArtist);
        }

        await recordingManager.StoreWithoutFiles(releaseAppends, BaseFolder);

        jobDispatcher.DispatchJob<MusicDescriptionJob>(releaseAppends.MusicBrainzReleaseGroup);

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = ["music", "albums"]
        });
    }
}