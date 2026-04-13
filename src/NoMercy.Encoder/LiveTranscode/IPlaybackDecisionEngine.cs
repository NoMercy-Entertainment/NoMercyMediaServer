namespace NoMercy.Encoder.LiveTranscode;

using NoMercy.Encoder.Analysis;

public interface IPlaybackDecisionEngine
{
    PlaybackDecision Decide(MediaInfo media, ClientCapabilities client);

    PlaybackDecision[] DecideBatch(MediaInfo[] library, ClientCapabilities client);
}
