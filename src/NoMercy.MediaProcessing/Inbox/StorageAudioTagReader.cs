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

using NoMercy.Storage;
using TagLib;

namespace NoMercy.MediaProcessing.Inbox;

public sealed class StorageAudioTagReader : IInboxAudioTagReader
{
    private readonly IStorageFactory _storageFactory;

    public StorageAudioTagReader(IStorageFactory storageFactory)
    {
        _storageFactory = storageFactory;
    }

    public async Task<InboxAudioTags?> ReadAsync(string path, Ulid driverId, CancellationToken ct)
    {
        try
        {
            IStorage storage = _storageFactory.For(folderId: Ulid.Empty, driverId: driverId, subPath: string.Empty);
            await using LocalPathLease lease = await storage.AcquireLocalPathAsync(path: path, ct: ct);

            using TagLib.File tagFile = TagLib.File.Create(path: lease.Path);
            Tag? tag = tagFile.Tag;

            if (tag is null)
                return null;

            Guid? releaseId = null;
            if (Guid.TryParse(input: tag.MusicBrainzReleaseId, result: out Guid parsedId))
                releaseId = parsedId;

            return new()
            {
                MusicBrainzReleaseId = releaseId == Guid.Empty ? null : releaseId,
                Album = string.IsNullOrWhiteSpace(value: tag.Album) ? null : tag.Album,
                Artist = string.IsNullOrWhiteSpace(value: tag.FirstPerformer) ? null : tag.FirstPerformer,
            };
        }
        catch
        {
            return null;
        }
    }
}
