using System.Collections.Concurrent;
using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Extensions;

public static class ConcurrentBag
{
    public static ConcurrentBag<T> ToConcurrentBag<T>(this IEnumerable<T> self) where T : class
    {
        if (self is ConcurrentBag<T> concurrentBag) return concurrentBag;
        return new(self);
    }

    public static ConcurrentBag<MediaFile> FilterConcurrentBag(this ConcurrentBag<MediaFile> self, string[] filterFiles)
    {
        self = self.Where(f => filterFiles.Any(s => f.Name == s || f.Path.Contains(s))).ToConcurrentBag();
        return self;
    }
}