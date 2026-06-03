using System.Collections.Concurrent;

namespace NoMercy.OpticalMedia.Live;

public class DiscSessionRegistry : IDiscSessionRegistry
{
    private readonly ConcurrentDictionary<string, string> _map = new(
        StringComparer.OrdinalIgnoreCase
    );

    public void Register(string drivePath, string sessionId) => _map[drivePath] = sessionId;

    public bool TryGet(string drivePath, out string sessionId) =>
        _map.TryGetValue(drivePath, out sessionId!);

    public void Remove(string drivePath) => _map.TryRemove(drivePath, out _);
}
