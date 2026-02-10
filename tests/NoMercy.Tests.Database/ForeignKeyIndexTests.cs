using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database.Models;

namespace NoMercy.Tests.Database;

public class ForeignKeyIndexTests
{
    private static bool HasIndex(Type type, string propertyName, bool isUnique = false)
    {
        IEnumerable<IndexAttribute> indexAttributes = type.GetCustomAttributes<IndexAttribute>();
        foreach (IndexAttribute attr in indexAttributes)
        {
            if (attr.PropertyNames.Count == 1 && attr.PropertyNames[0] == propertyName)
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
        bool hasIndex = HasIndex(typeof(Metadata), nameof(Metadata.AudioTrackId), isUnique: true);
        Assert.True(hasIndex, "Metadata should have a unique [Index] on AudioTrackId");
    }

    [Fact]
    public void Playlist_HasIndex_OnUserId()
    {
        bool hasIndex = HasIndex(typeof(Playlist), nameof(Playlist.UserId));
        Assert.True(hasIndex, "Playlist should have an [Index] on UserId");
    }

    [Fact]
    public void ActivityLog_HasIndex_OnUserId()
    {
        bool hasIndex = HasIndex(typeof(ActivityLog), nameof(ActivityLog.UserId));
        Assert.True(hasIndex, "ActivityLog should have an [Index] on UserId");
    }

    [Fact]
    public void ActivityLog_HasIndex_OnDeviceId()
    {
        bool hasIndex = HasIndex(typeof(ActivityLog), nameof(ActivityLog.DeviceId));
        Assert.True(hasIndex, "ActivityLog should have an [Index] on DeviceId");
    }

    [Fact]
    public void Collection_HasIndex_OnLibraryId()
    {
        bool hasIndex = HasIndex(typeof(Collection), nameof(Collection.LibraryId));
        Assert.True(hasIndex, "Collection should have an [Index] on LibraryId");
    }
}
