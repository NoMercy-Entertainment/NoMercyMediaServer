namespace NoMercy.Encoder.BuildingBlocks;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.PostProcess;

public interface ISubtitleExtractor
{
    SubtitleOutputInfo ResolveOutput(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string outputDirectory,
        string mediaTitle
    );

    string ResolvePlaylistUri(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string mediaTitle
    );
}
