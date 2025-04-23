// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddRecordingJob : AbstractMusicRecordingJob
{
    public override string QueueName => "queue";
    public override int Priority => 5;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();
        
        MusicGenreRepository musicGenreRepository = new(context);
        
        ArtistRepository artistRepository = new(context);
        ArtistManager artistManager = new(artistRepository, musicGenreRepository, jobDispatcher);
        
        RecordingRepository recordingRepository = new(context);
        RecordingManager recordingManager = new(recordingRepository, musicGenreRepository);
        
        await using MediaScan mediaScan = new();
        
        mediaScan.FilterByFileName(Container.FileName);
        
        MediaFolderExtend mediaFolder = (await mediaScan.DisableRegexFilter().EnableFileListing().Process(FolderMetaData.BasePath)).First();

        mediaFolder.Files?.FilterConcurrentBag([Container.FileName]);
        
        CoverArtImageManagerManager.CoverPalette? coverPalette = await CoverArtImageManagerManager.Add(FolderMetaData.MusicBrainzRelease.Id);
        
        await Parallel.ForEachAsync(FolderMetaData.MusicBrainzRelease.Media, async (media, t) =>
        {
            if (!await recordingManager.Store(FolderMetaData.MusicBrainzRelease, FoundTrack, media,
                Folder, mediaFolder, coverPalette)) return;
                
            Library? albumLibrary = Folder.FolderLibraries
                ?.FirstOrDefault(f => f.LibraryId == LibraryId)?.Library;
            
            if (albumLibrary is null)
            {
                Logger.MusicBrainz($"Album Library not found: {LibraryId}", LogEventLevel.Error);
                return;
            }
            
            await Parallel.ForEachAsync(FoundTrack.ArtistCredit, t, async (artist, _) =>
            {
                Logger.MusicBrainz($"Storing Artist: {artist.MusicBrainzArtist.Name}", LogEventLevel.Verbose);
                await artistManager.Store(artist.MusicBrainzArtist, albumLibrary, Folder, mediaFolder, FoundTrack);

                jobDispatcher.DispatchJob<MusicDescriptionJob>(artist.MusicBrainzArtist);
            });
        });
        
        Logger.App($"Recording {FoundTrack.Title} added to the library: {LibraryId}");

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto()
        {
            QueryKey = ["libraries", LibraryId.ToString()]
        });

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = ["music", FoundTrack.Id.ToString()]
        });
    }
}