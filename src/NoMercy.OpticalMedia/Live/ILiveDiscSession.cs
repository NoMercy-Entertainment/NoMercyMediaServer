using NoMercy.Encoder.LiveTranscode;
using NoMercy.OpticalMedia.Drives;

namespace NoMercy.OpticalMedia.Live;

/// <summary>
/// Bridges an optical-disc title into the existing live HLS encoder.
/// Each instance owns one ffmpeg-fed live session — call
/// <see cref="StartAsync"/> with a drive + title index, get back an
/// <see cref="ILiveSession"/> the web player can pull HLS from.
/// </summary>
public interface ILiveDiscSession
{
    Task<ILiveSession> StartAsync(
        DiscDrive drive,
        int titleIndex,
        TimeSpan startPosition,
        string? preferredQuality,
        CancellationToken ct
    );
}
