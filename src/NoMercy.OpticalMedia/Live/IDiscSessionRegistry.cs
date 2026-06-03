namespace NoMercy.OpticalMedia.Live;

/// <summary>
/// Tracks the live HLS session id that is currently active for each optical
/// drive path. Used by OpticalMediaController to map a drive path to the
/// session id so StopMedia can tear down the right session.
/// </summary>
public interface IDiscSessionRegistry
{
    void Register(string drivePath, string sessionId);
    bool TryGet(string drivePath, out string sessionId);
    void Remove(string drivePath);
}
