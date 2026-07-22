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

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Users;

namespace NoMercy.Tests.Database;

public class ForeignKeyIndexTests
{
    private static bool HasIndex(Type type, string propertyName, bool isUnique = false)
    {
        IEnumerable<IndexAttribute> indexAttributes = type.GetCustomAttributes<IndexAttribute>();
        foreach (IndexAttribute attr in indexAttributes)
        {
            if (attr.PropertyNames.Count == 1 && attr.PropertyNames[index: 0] == propertyName)
            {
                if (isUnique && !attr.IsUnique)
                    return false;
                return true;
            }
        }
        return false;
    }

    [Fact]
    public void Metadata_HasIndex_OnAudioTrackId()
    {
        bool hasIndex = HasIndex(type: typeof(Metadata), propertyName: nameof(Metadata.AudioTrackId), isUnique: true);
        Assert.True(condition: hasIndex, userMessage: "Metadata should have a unique [Index] on AudioTrackId");
    }

    [Fact]
    public void Playlist_HasIndex_OnUserId()
    {
        bool hasIndex = HasIndex(type: typeof(Playlist), propertyName: nameof(Playlist.UserId));
        Assert.True(condition: hasIndex, userMessage: "Playlist should have an [Index] on UserId");
    }

    [Fact]
    public void ActivityLog_HasIndex_OnUserId()
    {
        bool hasIndex = HasIndex(type: typeof(ActivityLog), propertyName: nameof(ActivityLog.UserId));
        Assert.True(condition: hasIndex, userMessage: "ActivityLog should have an [Index] on UserId");
    }

    [Fact]
    public void ActivityLog_HasIndex_OnDeviceId()
    {
        bool hasIndex = HasIndex(type: typeof(ActivityLog), propertyName: nameof(ActivityLog.DeviceId));
        Assert.True(condition: hasIndex, userMessage: "ActivityLog should have an [Index] on DeviceId");
    }

    [Fact]
    public void Collection_HasIndex_OnLibraryId()
    {
        bool hasIndex = HasIndex(type: typeof(Collection), propertyName: nameof(Collection.LibraryId));
        Assert.True(condition: hasIndex, userMessage: "Collection should have an [Index] on LibraryId");
    }
}
