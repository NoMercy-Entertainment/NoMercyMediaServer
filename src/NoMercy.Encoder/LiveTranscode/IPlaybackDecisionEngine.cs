using NoMercy.Encoder.Analysis;

namespace NoMercy.Encoder.LiveTranscode;

public interface IPlaybackDecisionEngine
{
    PlaybackDecision Decide(MediaInfo media, ClientCapabilities client);

    PlaybackDecision[] DecideBatch(MediaInfo[] library, ClientCapabilities client);
}
