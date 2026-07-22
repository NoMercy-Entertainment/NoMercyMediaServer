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
            .Include(navigationPropertyPath: f => f.EncodingPresetFolders)
            .Include(navigationPropertyPath: f => f.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Library)
            .Where(predicate: f => f.FolderLibraries.Any(fl => fl.Library.Type == detectedType))
            .ToListAsync(cancellationToken: ct);

        List<InboxDestination> destinations = [];

        foreach (Folder folder in folders)
        {
            FolderLibrary? folderLibrary = folder.FolderLibraries.FirstOrDefault(predicate: fl =>
                fl.Library.Type == detectedType
            );

            if (folderLibrary is null)
                continue;

            EncodingPresetFolder? presetFolder = folder
                .EncodingPresetFolders.OrderByDescending(keySelector: link => link.IsDefault)
                .FirstOrDefault();

            destinations.Add(
                item: new()
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
            detectedType: classification.DetectedType,
            context: context,
            ct: ct
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
                Destination = destinations[index: 0],
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
            throw new InvalidOperationException(message: "ExecuteAuto called on a non-auto RouteOutcome");

        InboxItem item = outcome.Item;
        CandidateMatch topCandidate = item.Candidates[0];

        await ExecuteMoveAndImport(item: item, match: topCandidate, destination: outcome.Destination, context: context, ct: ct);
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
        await ExecuteMoveAndImport(item: item, match: match, destination: destination, context: context, ct: ct);
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

        string fileName = GetFileName(path: item.SourcePath);

        // Scope the destination storage to the folder path so relative file names
        // resolve under the destination folder, not the process working directory.
        IStorage destStorage = _storageFactory.For(
            folderId: destination.FolderId,
            driverId: destination.DriverId,
            subPath: destination.FolderPath
        );

        if (item.DriverId == destination.DriverId)
        {
            // Same driver: both paths are on the same filesystem. Use GetFullPath to
            // obtain the absolute destination path, then move via the driver directly.
            // item.SourcePath is already an absolute OS path; fileName is the leaf name
            // that ResolveAgainstScopedRoot will join to the scoped root.
            string destAbsolute = destStorage.GetFullPath(path: fileName);
            string? destParent = Path.GetDirectoryName(path: destAbsolute);
            if (!string.IsNullOrEmpty(value: destParent))
                destStorage.Driver.CreateDirectory(path: destParent);
            destStorage.Driver.MoveFile(source: item.SourcePath, destination: destAbsolute);
        }
        else
        {
            IStorage sourceStorage = _storageFactory.For(folderId: Ulid.Empty, driverId: item.DriverId, subPath: string.Empty);

            byte[] bytes = await sourceStorage.ReadAsync(path: item.SourcePath, ct: ct);
            await destStorage.WriteAsync(path: fileName, bytes: bytes, ct: ct);

            long writtenSize = await destStorage.SizeAsync(path: fileName, ct: ct);
            long sourceSize = await sourceStorage.SizeAsync(path: item.SourcePath, ct: ct);

            if (writtenSize != sourceSize)
                throw new InvalidOperationException(
                    message: $"Cross-driver copy size mismatch: source={sourceSize} destination={writtenSize}"
                );

            await sourceStorage.DeleteAsync(path: item.SourcePath, ct: ct);
        }

        string movedPath = destination.FolderPath.TrimEnd(trimChar: '/') + "/" + fileName;

        DispatchImportJob(detectedType: item.DetectedType, topCandidate: match, destination: destination, movedPath: movedPath);

        item.Status = "Imported";
        item.TargetLibraryId = destination.LibraryId;
        item.TargetFolderId = destination.FolderId;
        item.TargetProfileId = destination.ProfileId == Ulid.Empty ? null : destination.ProfileId;
        item.SelectedMatch = match;

        if (context.Entry(entity: item).State == EntityState.Detached)
            context.InboxItems.Add(entity: item);

        await context.SaveChangesAsync(cancellationToken: ct);
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
                    id: int.Parse(s: topCandidate.ExternalId),
                    libraryId: destination.LibraryId
                );
                break;

            case "tv":
            case "anime":
                _jobDispatcher.DispatchJob<ShowImportJob>(
                    id: int.Parse(s: topCandidate.ExternalId),
                    libraryId: destination.LibraryId
                );
                break;

            case "music":
                _jobDispatcher.DispatchJob<AudioImportJob>(
                    libraryId: destination.LibraryId,
                    folderId: destination.FolderId,
                    filePath: movedPath
                );
                break;

            default:
                throw new InvalidOperationException(
                    message: $"No import job defined for detected type '{detectedType}'"
                );
        }
    }

    private static string GetFileName(string path)
    {
        string trimmed = path.TrimEnd(trimChars: ['/', '\\']);
        int slashIdx = trimmed.LastIndexOf(value: '/');
        int backslashIdx = trimmed.LastIndexOf(value: '\\');
        int idx = Math.Max(val1: slashIdx, val2: backslashIdx);
        return idx < 0 ? trimmed : trimmed[(idx + 1)..];
    }
}
