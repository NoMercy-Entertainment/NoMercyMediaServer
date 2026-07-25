// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Inbox;

public class InboxRoutingService
{
    private readonly IStorageFactory _storageFactory;
    private readonly JobDispatcher _jobDispatcher;

    public InboxRoutingService(IStorageFactory storageFactory, JobDispatcher jobDispatcher)
    {
        _storageFactory = storageFactory;
        _jobDispatcher = jobDispatcher;
    }

    /// <summary>
    /// Resolves valid destinations for the given detected media type.
    /// A destination is valid when a Library.Type == detectedType and the
    /// library has a Folder. Encoder-profile presence is no longer a routing
    /// requirement: auto-encode is a per-library decision gated separately
    /// (see <see cref="NoMercy.MediaProcessing.EventHandlers.AutoEncodeSubscriber"/>),
    /// so a type-matching folder with no V1 EncoderProfileFolder still routes.
    /// Fetches flat then groups client-side to respect the SQLite no-APPLY rule.
    /// </summary>
    public async Task<List<InboxDestination>> ResolveDestinations(
        string detectedType,
        MediaContext context,
        CancellationToken ct = default
    )
    {
        List<Folder> folders = await context
            .Folders.AsNoTracking()
            .Include(f => f.EncodingPresetFolders)
            .Include(f => f.FolderLibraries)
                .ThenInclude(fl => fl.Library)
            .Where(f => f.FolderLibraries.Any(fl => fl.Library.Type == detectedType))
            .ToListAsync(ct);

        List<InboxDestination> destinations = [];

        foreach (Folder folder in folders)
        {
            FolderLibrary? folderLibrary = folder.FolderLibraries.FirstOrDefault(fl =>
                fl.Library.Type == detectedType
            );

            if (folderLibrary is null)
                continue;

            EncodingPresetFolder? presetFolder = folder
                .EncodingPresetFolders.OrderByDescending(link => link.IsDefault)
                .FirstOrDefault();

            destinations.Add(
                new()
                {
                    LibraryId = folderLibrary.LibraryId,
                    FolderId = folder.Id,
                    ProfileId = presetFolder?.PresetId ?? Ulid.Empty,
                    DriverId = folder.DriverId,
                    FolderPath = folder.Path,
                }
            );
        }

        return destinations;
    }

    /// <summary>
    /// Decides auto-route vs review given a classification result and source path.
    /// AUTO only when: confidence == "high", single candidate, exactly one valid destination.
    /// Always builds an InboxItem regardless of the path taken.
    /// </summary>
    public async Task<RouteOutcome> Route(
        ClassificationResult classification,
        string sourcePath,
        Ulid driverId,
        MediaContext context,
        CancellationToken ct = default
    )
    {
        List<InboxDestination> destinations = await ResolveDestinations(
            classification.DetectedType,
            context,
            ct
        );

        InboxItem item = new()
        {
            Id = Ulid.NewUlid(),
            SourcePath = sourcePath,
            DriverId = driverId,
            DetectedType = classification.DetectedType,
            Confidence = classification.Confidence,
            Candidates = classification.Candidates,
        };

        bool isHighConfidence = classification.Confidence == "high";
        bool hasSingleCandidate = classification.Candidates.Length >= 1;
        bool hasSingleDestination = destinations.Count == 1;

        if (isHighConfidence && hasSingleCandidate && hasSingleDestination)
        {
            item.Status = "Routing";
            return new()
            {
                Mode = "auto",
                Destination = destinations[0],
                Item = item,
            };
        }

        item.Status = "NeedsReview";
        return new()
        {
            Mode = "review",
            Destination = null,
            Item = item,
        };
    }

    /// <summary>
    /// Carries out the auto-route: moves the source file into the destination folder,
    /// dispatches the matching import job, and updates InboxItem status.
    /// Same driver => MoveAsync within one IStorage scope.
    /// Different driver => CopyAsync then verify then DeleteAsync.
    /// Does NOT dispatch VideoEncodeJob — AutoEncodeSubscriber handles encoding
    /// after the import emits MediaFilesScannedEvent.
    /// </summary>
    public async Task ExecuteAuto(
        RouteOutcome outcome,
        MediaContext context,
        CancellationToken ct = default
    )
    {
        if (outcome.Mode != "auto" || outcome.Destination is null)
            throw new InvalidOperationException("ExecuteAuto called on a non-auto RouteOutcome");

        InboxItem item = outcome.Item;
        CandidateMatch topCandidate = item.Candidates[0];

        await ExecuteMoveAndImport(item, topCandidate, outcome.Destination, context, ct);
    }

    /// <summary>
    /// Carries out a user-driven assignment: moves the file into the chosen destination,
    /// dispatches the import job for the user's selected match, and updates InboxItem status.
    /// The caller is responsible for setting item.DetectedType, item.SelectedMatch, and
    /// the Target*Id fields before saving; this method only performs the file move and job dispatch.
    /// </summary>
    public async Task ExecuteAssignment(
        InboxItem item,
        CandidateMatch match,
        InboxDestination destination,
        MediaContext context,
        CancellationToken ct = default
    )
    {
        await ExecuteMoveAndImport(item, match, destination, context, ct);
    }

    private async Task ExecuteMoveAndImport(
        InboxItem item,
        CandidateMatch match,
        InboxDestination destination,
        MediaContext context,
        CancellationToken ct
    )
    {
        item.Status = "Routing";

        string fileName = GetFileName(item.SourcePath);

        // Scope the destination storage to the folder path so relative file names
        // resolve under the destination folder, not the process working directory.
        IStorage destStorage = _storageFactory.For(
            destination.FolderId,
            destination.DriverId,
            destination.FolderPath
        );

        if (item.DriverId == destination.DriverId)
        {
            // Same driver: both paths are on the same filesystem. Use GetFullPath to
            // obtain the absolute destination path, then move via the driver directly.
            // item.SourcePath is already an absolute OS path; fileName is the leaf name
            // that ResolveAgainstScopedRoot will join to the scoped root.
            string destAbsolute = destStorage.GetFullPath(fileName);
            string? destParent = Path.GetDirectoryName(destAbsolute);
            if (!string.IsNullOrEmpty(destParent))
                destStorage.Driver.CreateDirectory(destParent);
            destStorage.Driver.MoveFile(item.SourcePath, destAbsolute);
        }
        else
        {
            IStorage sourceStorage = _storageFactory.For(Ulid.Empty, item.DriverId, string.Empty);

            byte[] bytes = await sourceStorage.ReadAsync(item.SourcePath, ct);
            await destStorage.WriteAsync(fileName, bytes, ct);

            long writtenSize = await destStorage.SizeAsync(fileName, ct);
            long sourceSize = await sourceStorage.SizeAsync(item.SourcePath, ct);

            if (writtenSize != sourceSize)
                throw new InvalidOperationException(
                    $"Cross-driver copy size mismatch: source={sourceSize} destination={writtenSize}"
                );

            await sourceStorage.DeleteAsync(item.SourcePath, ct);
        }

        string movedPath = destination.FolderPath.TrimEnd('/') + "/" + fileName;

        DispatchImportJob(item.DetectedType, match, destination, movedPath);

        item.Status = "Imported";
        item.TargetLibraryId = destination.LibraryId;
        item.TargetFolderId = destination.FolderId;
        item.TargetProfileId = destination.ProfileId == Ulid.Empty ? null : destination.ProfileId;
        item.SelectedMatch = match;

        if (context.Entry(item).State == EntityState.Detached)
            context.InboxItems.Add(item);

        await context.SaveChangesAsync(ct);
    }

    private void DispatchImportJob(
        string detectedType,
        CandidateMatch topCandidate,
        InboxDestination destination,
        string movedPath
    )
    {
        switch (detectedType)
        {
            case "movie":
                _jobDispatcher.DispatchJob<MovieImportJob>(
                    int.Parse(topCandidate.ExternalId),
                    destination.LibraryId
                );
                break;

            case "tv":
            case "anime":
                _jobDispatcher.DispatchJob<ShowImportJob>(
                    int.Parse(topCandidate.ExternalId),
                    destination.LibraryId
                );
                break;

            case "music":
                _jobDispatcher.DispatchJob<AudioImportJob>(
                    destination.LibraryId,
                    destination.FolderId,
                    movedPath
                );
                break;

            default:
                throw new InvalidOperationException(
                    $"No import job defined for detected type '{detectedType}'"
                );
        }
    }

    private static string GetFileName(string path)
    {
        string trimmed = path.TrimEnd('/', '\\');
        int slashIdx = trimmed.LastIndexOf('/');
        int backslashIdx = trimmed.LastIndexOf('\\');
        int idx = Math.Max(slashIdx, backslashIdx);
        return idx < 0 ? trimmed : trimmed[(idx + 1)..];
    }
}
