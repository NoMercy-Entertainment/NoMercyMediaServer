using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.MediaProcessing.ReleaseGroups;
using NoMercy.MediaProcessing.Releases;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class AudioImportJob : AbstractMusicFolderJob
{
    public override string QueueName => "import";
    public override int Priority => 6;

    private MediaFolderExtend? _rootFolder;

    private MediaContext? _mediaContext;
    
    public override async Task Handle()
    {
        if (InputFolder.Contains("[Singles]"))
        {
            await ImportSingles();
        }
        else
        {
            await ImportRelease();
        }
    }

    private async Task ImportSingles()
    {
        Init(
            out MusicBrainzReleaseClient musicBrainzReleaseClient,
            out MusicBrainzArtistClient musicBrainzArtistClient,
            out MusicBrainzRecordingClient musicBrainzRecordingClient,
            out ReleaseGroupManager releaseGroupManager,
            out ReleaseManager releaseManager,
            out ArtistManager artistManager,
            out RecordingManager recordingManager,
            out MusicGenreManager musicGenreManager,
            out Library albumLibrary,
            out Folder folderLibrary,
            out Func<IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)>> audioFilesFactory,
            out Dictionary<Guid, (MusicBrainzReleaseAppends ReleaseAppends, int Count)> _,
            out JobDispatcher jobDispatcher
        );
        using (musicBrainzReleaseClient) using (musicBrainzArtistClient) using (musicBrainzRecordingClient)
        await using (_mediaContext)
        {
            bool wasEmpty = !await _mediaContext!.AlbumLibrary.AnyAsync(al => al.LibraryId == LibraryId);

            Dictionary<Guid, (MusicBrainzReleaseAppends SingleAppends,
                List<(MediaFile MediaFile, AudioTagModel audioTagModel)> File)> processedSingles = new();
            await foreach ((MediaFile mediaFile, AudioTagModel audioTag) in audioFilesFactory())
            {
                if (audioTag.MusicBrainz?.ReleaseId is null || audioTag.MusicBrainz.ReleaseId == Guid.Empty)
                    continue;

                MusicBrainzReleaseAppends? releaseAppends =
                    await musicBrainzReleaseClient.WithAllAppends(audioTag.MusicBrainz.ReleaseId);
                if (releaseAppends is null)
                    continue;

                if (processedSingles.TryGetValue(audioTag.MusicBrainz.ReleaseId,
                        out (MusicBrainzReleaseAppends SingleAppends,
                        List<(MediaFile MediaFile, AudioTagModel audioTagModel)> File) value))
                {
                    value.File.Add((mediaFile, audioTag));
                }
                else
                {
                    processedSingles.Add(audioTag.MusicBrainz.ReleaseId, (releaseAppends, [(mediaFile, audioTag)]));
                }
            }

            foreach ((MusicBrainzReleaseAppends singleRelease,
                         List<(MediaFile mediaFile, AudioTagModel audioTagModel)> files) in processedSingles.Values)
            {
                await AddSingleOrRelease(singleRelease, musicGenreManager, releaseGroupManager, releaseManager,
                    albumLibrary, folderLibrary, files, musicBrainzArtistClient, artistManager, jobDispatcher, musicBrainzRecordingClient,
                    recordingManager);

                jobDispatcher.DispatchJob<MusicMetadataJob>(singleRelease.MusicBrainzReleaseGroup);
                await SendRefresh(["music", "start"]);
            }

            if (wasEmpty && processedSingles.Count > 0)
                await SendRefresh(["libraries"]);
        }
        try { musicBrainzReleaseClient.Dispose(); } catch (Exception disposeEx) { Logger.Error($"Dispose failed: {disposeEx}"); }
        try { musicBrainzArtistClient.Dispose(); } catch (Exception disposeEx) { Logger.Error($"Dispose failed: {disposeEx}"); }
        try { musicBrainzRecordingClient.Dispose(); } catch (Exception disposeEx) { Logger.Error($"Dispose failed: {disposeEx}"); }
        try
        {
            if (_mediaContext != null) await _mediaContext.DisposeAsync();
        } catch (Exception disposeEx) { Logger.Error($"Dispose failed: {disposeEx}"); }
        _mediaContext = null;
    }

    private async Task ImportRelease()
    {
        Init(
            out MusicBrainzReleaseClient musicBrainzReleaseClient,
            out MusicBrainzArtistClient musicBrainzArtistClient,
            out MusicBrainzRecordingClient musicBrainzRecordingClient,
            out ReleaseGroupManager releaseGroupManager,
            out ReleaseManager releaseManager,
            out ArtistManager artistManager,
            out RecordingManager recordingManager,
            out MusicGenreManager musicGenreManager,
            out Library albumLibrary,
            out Folder folderLibrary,
            out Func<IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)>> audioFilesFactory,
            out Dictionary<Guid, (MusicBrainzReleaseAppends ReleaseAppends, int Count)> releases,
            out JobDispatcher jobDispatcher
        );
        using (musicBrainzReleaseClient) using (musicBrainzArtistClient) using (musicBrainzRecordingClient)
        await using (_mediaContext)
        {
            bool wasEmpty = !await _mediaContext!.AlbumLibrary.AnyAsync(al => al.LibraryId == LibraryId);

            // First pass: count releases without storing all tags in memory
            await foreach ((_, AudioTagModel audioTag) in audioFilesFactory())
            {
                if (audioTag.MusicBrainz?.ReleaseId is null || audioTag.MusicBrainz.ReleaseId == Guid.Empty)
                    continue;

                MusicBrainzReleaseAppends? releaseAppends =
                    await musicBrainzReleaseClient.WithAllAppends(audioTag.MusicBrainz.ReleaseId);
                if (releaseAppends is null)
                    continue;

                if (releases.TryGetValue(releaseAppends.Id,
                        out (MusicBrainzReleaseAppends ReleaseAppends, int Count) value))
                    releases[releaseAppends.Id] = (releaseAppends, value.Count + 1);
                else
                    releases.Add(releaseAppends.Id, (releaseAppends, 1));
            }

            // pick the most common release
            MusicBrainzReleaseAppends? release =
                releases.OrderByDescending(x => x.Value.Count).FirstOrDefault().Value.ReleaseAppends;
            if (release is null)
                return;

            // Second pass: collect only files that match the chosen release
            List<(MediaFile MediaFile, AudioTagModel AudioTag)> matchingFiles = [];
            await foreach ((MediaFile mediaFile, AudioTagModel audioTag) in audioFilesFactory())
            {
                if (audioTag.MusicBrainz?.ReleaseId == release.Id ||
                    (audioTag.MusicBrainz?.ReleaseTrackId != null &&
                     release.Media.Any(m =>
                         m.Tracks.Any(t =>
                             t.Id == audioTag.MusicBrainz.ReleaseTrackId ||
                             t.Id == audioTag.MusicBrainz.RecordingId ||
                             t.Recording.Id == audioTag.MusicBrainz.RecordingId ||
                             t.Recording.Id == audioTag.MusicBrainz.ReleaseTrackId))))
                {
                    matchingFiles.Add((mediaFile, audioTag));
                }
            }

            await AddSingleOrRelease(release, musicGenreManager, releaseGroupManager, releaseManager, albumLibrary,
                folderLibrary, matchingFiles, musicBrainzArtistClient, artistManager, jobDispatcher, musicBrainzRecordingClient,
                recordingManager);

            jobDispatcher.DispatchJob<MusicMetadataJob>(release.MusicBrainzReleaseGroup);
            await SendRefresh(["music", "start"]);

            if (wasEmpty)
                await SendRefresh(["libraries"]);
        }
        try { musicBrainzReleaseClient.Dispose(); } catch (Exception disposeEx) { Logger.Error($"Dispose failed: {disposeEx}"); }
        try { musicBrainzArtistClient.Dispose(); } catch (Exception disposeEx) { Logger.Error($"Dispose failed: {disposeEx}"); }
        try { musicBrainzRecordingClient.Dispose(); } catch (Exception disposeEx) { Logger.Error($"Dispose failed: {disposeEx}"); }
        try
        {
            if (_mediaContext != null) await _mediaContext.DisposeAsync();
        } catch (Exception disposeEx) { Logger.Error($"Dispose failed: {disposeEx}"); }
        _mediaContext = null;
    }

    private static async Task SendRefresh(dynamic[] query)
    {
        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = query
            });
    }

    private async Task AddSingleOrRelease(MusicBrainzReleaseAppends release, MusicGenreManager musicGenreManager,
        ReleaseGroupManager releaseGroupManager, ReleaseManager releaseManager, Library albumLibrary, Folder folderLibrary,
        List<(MediaFile MediaFile, AudioTagModel AudioTag)> audioFiles, MusicBrainzArtistClient musicBrainzArtistClient, ArtistManager artistManager,
        JobDispatcher jobDispatcher, MusicBrainzRecordingClient musicBrainzRecordingClient,
        RecordingManager recordingManager)
    {
        CoverArtImageManagerManager.CoverPalette? coverPalette = await CoverArtImageManagerManager.Add(release.MusicBrainzReleaseGroup.Id, true);
        if (coverPalette is not null)
        {
            using Image<Rgba32>? downloadedImage = await CoverArtCoverArtClient.Download(coverPalette.Url);
        }
            
        await AddGenres(release.Genres, musicGenreManager);
        
        await releaseGroupManager.Store(release.MusicBrainzReleaseGroup, LibraryId, coverPalette);
        await releaseManager.Store(release, albumLibrary, folderLibrary, audioFiles.First().MediaFile, coverPalette);
        
        foreach (ReleaseArtistCredit artistCredit in release.ArtistCredit)
        {
            MusicBrainzArtistAppends? artistDetails = await musicBrainzArtistClient.WithAllAppends(artistCredit.MusicBrainzArtist.Id);
            if (artistDetails is null) continue;
            await artistManager.Store(artistDetails, release, albumLibrary, folderLibrary);
            jobDispatcher.DispatchJob<MusicMetadataJob>(artistDetails);
            await SendRefresh(["music", "artist", artistDetails.Id]);
        }
        
        List<MusicBrainzTrack> allTracks = release.Media
            .SelectMany(m => m.Tracks)
            .ToList();

        for (int index = 0; index < allTracks.Count; index++)
        {
            MusicBrainzTrack musicBrainzTrack = allTracks[index];
            
            int idx = release.Media
                .ToList()
                .FindIndex(t => t.Tracks.All(w => w.Id == musicBrainzTrack.Id));
            
            MediaFile? mediaFile = null;
            AudioTagModel? audioTag = null;
            foreach ((MediaFile file, AudioTagModel tag) in audioFiles)
            {
                if ((tag.MusicBrainz?.ReleaseTrackId != musicBrainzTrack.Id &&
                     tag.MusicBrainz?.ReleaseTrackId != musicBrainzTrack.Recording.Id &&
                     tag.MusicBrainz?.RecordingId != musicBrainzTrack.Id &&
                     tag.MusicBrainz?.RecordingId != musicBrainzTrack.Recording.Id) ||
                    (!musicBrainzTrack.Title.ContainsSanitized(tag.Tags?.Title ?? file.Parsed?.Title) &&
                     !(Math.Abs(tag.Duration - musicBrainzTrack.Duration) < 5) &&
                     musicBrainzTrack.Position != tag.Tags?.Track &&
                     musicBrainzTrack.Position != file.Parsed?.TrackNumber &&
                     musicBrainzTrack.Position != index + 1 &&
                     musicBrainzTrack.Position * idx != index + 1)) continue;
                mediaFile = file;
                audioTag = tag;
                break;
            }
            if (mediaFile is null || audioTag is null) continue;
            
            MusicBrainzRecordingAppends? musicBrainzRecording = await musicBrainzRecordingClient.WithAllAppends(musicBrainzTrack.Recording.Id);
            if (musicBrainzRecording is null) continue;
            
            await AddGenres(musicBrainzRecording.Genres, musicGenreManager);
                
            await recordingManager.Store(release, musicBrainzTrack, [], mediaFile, folderLibrary, coverPalette);
                
            foreach (MusicBrainzArtistCredit artistCredit in musicBrainzRecording.ArtistCredit)
            {
                if (_rootFolder is null)
                    continue;
                MusicBrainzArtistAppends? artistDetails = await musicBrainzArtistClient.WithAllAppends(artistCredit.MusicBrainzArtist.Id);
                if (artistDetails is null) continue;
                await artistManager.Store(artistDetails, albumLibrary, folderLibrary, _rootFolder!, musicBrainzTrack);
                jobDispatcher.DispatchJob<MusicMetadataJob>(artistDetails);
                await SendRefresh(["music", "artist", artistDetails.Id]);
            }
        }
        
        await SendRefresh(["music", "album", release.Id]);
    }

    private static async Task AddGenres(MusicBrainzGenreDetails[] genres, MusicGenreManager musicGenreManager)
    {
        foreach (MusicBrainzGenreDetails musicBrainzGenreDetails in genres)
            await musicGenreManager.Store(musicBrainzGenreDetails);
    }

    private void Init(
        out MusicBrainzReleaseClient musicBrainzReleaseClient, 
        out MusicBrainzArtistClient musicBrainzArtistClient, 
        out MusicBrainzRecordingClient musicBrainzRecordingClient, 
        out ReleaseGroupManager releaseGroupManager, 
        out ReleaseManager releaseManager, 
        out ArtistManager artistManager, 
        out RecordingManager recordingManager,
        out MusicGenreManager musicGenreManager,
        out Library albumLibrary, 
        out Folder folderLibrary, 
        out Func<IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)>> audioFilesFactory, 
        out Dictionary<Guid, (MusicBrainzReleaseAppends ReleaseAppends, int Count)> releases,
        out JobDispatcher jobDispatcher
    )
    {
        _mediaContext = new();
        jobDispatcher = new();
        
        musicBrainzReleaseClient = new();
        musicBrainzArtistClient = new();
        musicBrainzRecordingClient = new();
    
        releases = new();
    
        ReleaseGroupRepository releaseGroupRepository = new(_mediaContext);
        releaseGroupManager = new(releaseGroupRepository);

        MusicGenreRepository musicGenreRepository = new(_mediaContext);
        musicGenreManager = new(musicGenreRepository);

        ReleaseRepository releaseRepository = new(_mediaContext);
        releaseManager = new(releaseRepository, musicGenreRepository);

        ArtistRepository artistRepository = new(_mediaContext);
        artistManager = new(artistRepository, musicGenreRepository, jobDispatcher);

        RecordingRepository recordingRepository = new(_mediaContext);
        recordingManager = new(recordingRepository, musicGenreRepository, artistRepository);

        albumLibrary = _mediaContext.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .First();
    
        folderLibrary = albumLibrary.FolderLibraries.First().Folder;
    
        audioFilesFactory = GetAudioFiles;
    }

    private async IAsyncEnumerable<(MediaFile MediaFile, AudioTagModel AudioTag)> GetAudioFiles()
    {
        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .DisableRegexFilter()
            .EnableFileListing()
            .Process(InputFolder, 1);
        
        _rootFolder ??= rootFolders.FirstOrDefault();

        IEnumerable<MediaFile> files = rootFolders.SelectMany(mediaFolderExtend => mediaFolderExtend.Files ?? Enumerable.Empty<MediaFile>());

        ConcurrentBag<(MediaFile, AudioTagModel)> bag = [];

        foreach (MediaFile mediaFile in files)
        {
            AudioTagModel audioTagModel = await AudioTagModel.Create(mediaFile);
            bag.Add((mediaFile, audioTagModel));
        }
        
        foreach ((MediaFile mediaFile, AudioTagModel audioTagModel) in bag)
            yield return (mediaFile, audioTagModel);
    }
}
