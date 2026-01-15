using System.Collections.Concurrent;

namespace NoMercy.NmSystem.Dto;

public class MediaFolderExtend : MediaFolder
{
    public ConcurrentBag<MediaFile>? Files { get; init; } = [];
    public ConcurrentBag<MediaFolderExtend>? SubFolders { get; init; } = [];
}