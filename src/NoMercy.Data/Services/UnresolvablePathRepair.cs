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
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Services;

/// <summary>
/// Removes media rows the scanner wrote before it validated its paths. A row
/// qualifies when its Folder is neither empty nor root-relative — an unstripped
/// host path such as <c>"M:/Download/complete/…"</c> — or when its Filename
/// names no file. Both compose into a <c>/{FolderId}{Folder}{Filename}</c> URL
/// that resolves for no client, so the entry is listed but unplayable, and a
/// rescan cannot repair it: the file it points at is outside every library root.
/// Rows only; files on disk are never touched, and the next library scan re-adds
/// anything that does live under a root.
/// </summary>
public class UnresolvablePathRepair(
    IDbContextFactory<MediaContext> mediaContextFactory,
    ILogger<UnresolvablePathRepair> logger
) : IUnresolvablePathRepair
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await using MediaContext mediaContext = await mediaContextFactory.CreateDbContextAsync(
            cancellationToken
        );

        List<Track> tracks = await mediaContext
            .Tracks.Where(track =>
                track.Filename == null
                || track.Filename == ""
                || track.Filename == "/"
                || (track.Folder != null && track.Folder != "" && !track.Folder.StartsWith("/"))
            )
            .ToListAsync(cancellationToken);

        List<VideoFile> videoFiles = await mediaContext
            .VideoFiles.Where(videoFile =>
                videoFile.Filename == null
                || videoFile.Filename == ""
                || videoFile.Filename == "/"
                || (
                    videoFile.Folder != null
                    && videoFile.Folder != ""
                    && !videoFile.Folder.StartsWith("/")
                )
            )
            .ToListAsync(cancellationToken);

        if (tracks.Count == 0 && videoFiles.Count == 0)
            return 0;

        foreach (Track track in tracks)
            logger.LogWarning(
                "Removing unplayable track {Name}: '{Folder}{Filename}' addresses no file",
                [track.Name, track.Folder, track.Filename]
            );

        foreach (VideoFile videoFile in videoFiles)
            logger.LogWarning(
                "Removing unplayable video file '{Folder}{Filename}': it addresses no file",
                [videoFile.Folder, videoFile.Filename]
            );

        await using IDbContextTransaction transaction =
            await mediaContext.Database.BeginTransactionAsync(cancellationToken);

        // Every child of Tracks is ON DELETE RESTRICT — deliberately, so a
        // single-track delete can never cascade through shared metadata — which
        // means the links have to go first. VideoFile children cascade and need
        // no equivalent.
        List<Guid> trackIds = tracks.Select(track => track.Id).ToList();
        await ClearRestrictedChildrenAsync(
            mediaContext,
            typeof(Track),
            trackIds,
            cancellationToken
        );

        mediaContext.Tracks.RemoveRange(tracks);
        mediaContext.VideoFiles.RemoveRange(videoFiles);
        await mediaContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return tracks.Count + videoFiles.Count;
    }

    /// <summary>
    /// Deletes the rows that a RESTRICT foreign key would otherwise use to block
    /// deleting the given parents. Reads the dependents from the EF model rather
    /// than a hand-kept list, so a child table added later is covered the day it
    /// is mapped instead of resurfacing this as a boot-time failure.
    /// </summary>
    private static async Task ClearRestrictedChildrenAsync(
        MediaContext mediaContext,
        Type parentType,
        List<Guid> parentIds,
        CancellationToken cancellationToken
    )
    {
        if (parentIds.Count == 0)
            return;

        IEntityType parent =
            mediaContext.Model.FindEntityType(parentType)
            ?? throw new InvalidOperationException($"{parentType.Name} is not a mapped entity.");

        string placeholders = string.Join(
            ",",
            Enumerable.Range(0, parentIds.Count).Select(index => $"{{{index}}}")
        );
        object[] parameters = parentIds.Cast<object>().ToArray();

        foreach (IForeignKey foreignKey in parent.GetReferencingForeignKeys())
        {
            if (foreignKey.DeleteBehavior != DeleteBehavior.Restrict)
                continue;

            IEntityType dependent = foreignKey.DeclaringEntityType;
            string? table = dependent.GetTableName();
            if (table is null)
                continue;

            StoreObjectIdentifier storeObject = StoreObjectIdentifier.Table(
                table,
                dependent.GetSchema()
            );
            string? column = foreignKey.Properties[0].GetColumnName(storeObject);
            if (column is null)
                continue;

            // Table and column are EF model metadata, never user input; the ids
            // are bound as parameters.
            string sql = string.Concat(
                "DELETE FROM \"",
                table,
                "\" WHERE \"",
                column,
                "\" IN (",
                placeholders,
                ")"
            );

            await mediaContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }
    }
}
