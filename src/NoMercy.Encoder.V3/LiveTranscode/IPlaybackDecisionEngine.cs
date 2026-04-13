namespace NoMercy.Encoder.V3.LiveTranscode;

using NoMercy.Encoder.V3.Analysis;

public interface IPlaybackDecisionEngine
{
    PlaybackDecision Decide(MediaInfo media, ClientCapabilities client);

    PlaybackDecision[] DecideBatch(MediaInfo[] library, ClientCapabilities client);
}
