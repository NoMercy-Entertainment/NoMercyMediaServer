// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.MediaProcessing.ReleaseGroups;
using NoMercy.MediaProcessing.Releases;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddReleaseJob : AbstractReleaseJob
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

        Library albumLibrary = await context.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();
        
        (MusicBrainzReleaseAppends? releaseAppends, CoverArtImageManagerManager.CoverPalette? coverPalette) = await releaseManager.Add(Id, albumLibrary, BaseFolder, MediaFolder);
        
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
        }
        
        foreach (MusicBrainzMedia media in releaseAppends.Media)
        foreach (MusicBrainzTrack track in media.Tracks)
        {
            if (!await recordingManager.Store(releaseAppends, track, media, BaseFolder, MediaFolder, coverPalette))
            {
                continue;
            }

            foreach (ReleaseArtistCredit artist in track.ArtistCredit)
            {
                await artistManager.Store(artist.MusicBrainzArtist, albumLibrary, BaseFolder, MediaFolder, track);
            }
        }
        
        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = ["music", "album", Id.ToString()]
        });
    }
}