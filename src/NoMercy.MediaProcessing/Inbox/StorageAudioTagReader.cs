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
            IStorage storage = _storageFactory.For(Ulid.Empty, driverId, string.Empty);
            await using LocalPathLease lease = await storage.AcquireLocalPathAsync(path, ct);

            using TagLib.File tagFile = TagLib.File.Create(lease.Path);
            Tag? tag = tagFile.Tag;

            if (tag is null)
                return null;

            Guid? releaseId = null;
            if (Guid.TryParse(tag.MusicBrainzReleaseId, out Guid parsedId))
                releaseId = parsedId;

            return new InboxAudioTags
            {
                MusicBrainzReleaseId = releaseId == Guid.Empty ? null : releaseId,
                Album = string.IsNullOrWhiteSpace(tag.Album) ? null : tag.Album,
                Artist = string.IsNullOrWhiteSpace(tag.FirstPerformer) ? null : tag.FirstPerformer,
            };
        }
        catch
        {
            return null;
        }
    }
}
