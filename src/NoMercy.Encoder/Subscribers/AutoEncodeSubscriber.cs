using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles.V2;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Storage;

namespace NoMercy.Encoder.Subscribers;

/// <summary>
/// Listens for <see cref="MediaFilesScannedEvent"/> and automatically starts
/// an encode when the affected media's folder is mapped to a profile in
/// <see cref="EncoderOptions.WatchedFolderProfiles"/>.
///
/// Opt-out: set <see cref="EncoderOptions.EnableAutoEncodeSubscriber"/> = false.
/// </summary>
public class AutoEncodeSubscriber : IDisposable
{
    private readonly IEncodingOrchestrator _orchestrator;
    private readonly EncoderOptions _options;
    private readonly ILogger<AutoEncodeSubscriber> _logger;
    private readonly List<IDisposable> _subscriptions = [];

    public AutoEncodeSubscriber(
        IEventBus eventBus,
        IEncodingOrchestrator orchestrator,
        EncoderOptions options,
        ILogger<AutoEncodeSubscriber> logger
    )
    {
        _orchestrator = orchestrator;
        _options = options;
        _logger = logger;

        _subscriptions.Add(eventBus.Subscribe<MediaFilesScannedEvent>(OnMediaFilesScanned));
    }

    internal async Task OnMediaFilesScanned(MediaFilesScannedEvent @event, CancellationToken ct)
    {
        if (!_options.EnableAutoEncodeSubscriber)
            return;

        if (_options.WatchedFolderProfiles.Count == 0)
            return;

        // Resolve the primary source file from the database.
        VideoFile? videoFile;
        await using (MediaContext context = new())
        {
            videoFile = await context
                .VideoFiles.AsNoTracking()
                .Where(vf => vf.EpisodeId == @event.MediaId || vf.MovieId == @event.MediaId)
                .OrderBy(vf => vf.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (videoFile is null)
        {
            _logger.LogDebug(
                "AutoEncode: no VideoFile found for media {MediaId} — skipping",
                @event.MediaId
            );
            return;
        }

        string filePath = videoFile.HostFolder + videoFile.Filename;

        // Find a profile whose watched-folder key is a path prefix of the source file.
        EncodingProfile? profile = null;
        string? matchedFolder = null;

        foreach (KeyValuePair<string, EncodingProfile> entry in _options.WatchedFolderProfiles)
        {
            if (
                filePath.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(filePath, entry.Key, StringComparison.OrdinalIgnoreCase)
            )
            {
                profile = entry.Value;
                matchedFolder = entry.Key;
                break;
            }
        }

        if (profile is null)
            return;

        string outputDirectory = StoragePathHelpers.Combine(
            StoragePathHelpers.GetParent(filePath) ?? string.Empty,
            StoragePathHelpers.GetNameWithoutExtension(filePath)
        );

        _logger.LogInformation(
            "AutoEncode: media {MediaId} matched watched folder {Folder} — dispatching encode",
            @event.MediaId,
            matchedFolder
        );

        EncodingRequest request = new(
            InputPath: filePath,
            OutputDirectory: outputDirectory,
            Profile: profile
        );

        try
        {
            await _orchestrator.EncodeAsync(request, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AutoEncode: encode failed for media {MediaId}", @event.MediaId);
        }
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();

        _subscriptions.Clear();
    }
}
