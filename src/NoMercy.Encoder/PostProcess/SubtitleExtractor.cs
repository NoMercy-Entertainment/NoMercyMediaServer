namespace NoMercy.Encoder.PostProcess;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Subtitles;

public class SubtitleExtractor : ISubtitleExtractor
{
    private static readonly HashSet<string> AssCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "ass",
        "ssa",
    };

    /// <summary>
    /// Resolves the output file path and FFmpeg codec for a subtitle stream.
    /// ASS/SSA subtitles stay as ASS. Other text subtitles convert to WebVTT.
    /// Bitmap subtitles are extracted as-is (VobSub → .sub+.idx, PGS/DVB → .sup).
    /// </summary>
    public SubtitleOutputInfo ResolveOutput(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string outputDirectory,
        string mediaTitle
    )
    {
        string language = stream.Language ?? plan.Language ?? "und";
        string variant = ResolveVariant(stream);
        bool isBitmap = SubtitleClassifier.IsBitmapBased(stream.Codec);
        bool isAss = AssCodecs.Contains(stream.Codec);

        string extension;
        string ffmpegCodec;

        if (isBitmap)
        {
            // VobSub gets its native .idx/.sub pair via NoMercy ffmpeg's
            // vobsubenc muxer. PGS / DVB stay in .mks for preservation —
            // OCR pass converts them to WebVTT downstream.
            bool isVobSub = stream.Codec.Equals("dvd_subtitle", StringComparison.OrdinalIgnoreCase);
            extension = isVobSub ? "idx" : "mks";
            ffmpegCodec = "copy";
        }
        else
        {
            // Honor the profile's requested output codec so HLS playlists
            // that reference .vtt sidecars get .vtt files even when the
            // source is .ass. ASS preservation is opt-in via Codec=Ass.
            (extension, ffmpegCodec) = plan.OutputCodec switch
            {
                SubtitleCodecType.WebVtt => ("vtt", "webvtt"),
                SubtitleCodecType.Ass => ("ass", "ass"),
                SubtitleCodecType.Srt => ("srt", "srt"),
                SubtitleCodecType.Copy => isAss ? ("ass", "copy") : ("vtt", "copy"),
                _ => ("vtt", "webvtt"),
            };
        }

        Dictionary<string, string> tokens = TemplateResolver.SubtitleTokens(
            language,
            variant,
            mediaTitle
        );
        string resolved = TemplateResolver.Resolve(plan.PlaylistNameTemplate, tokens);
        // Relative path — FFmpeg CWD is set to the output directory.
        string outputPath = $"{resolved}.{extension}";

        return new(
            OutputPath: outputPath,
            FfmpegCodec: ffmpegCodec,
            Extension: extension,
            Language: language,
            Variant: variant,
            IsBitmap: isBitmap,
            SourceIndex: plan.SourceIndex
        );
    }

    /// <summary>
    /// Resolves the URI for the master playlist's subtitle group entry.
    /// Path is relative to the output directory.
    /// </summary>
    public string ResolvePlaylistUri(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string mediaTitle
    )
    {
        string language = stream.Language ?? plan.Language ?? "und";
        string variant = ResolveVariant(stream);
        bool isBitmap = SubtitleClassifier.IsBitmapBased(stream.Codec);
        bool isAss = AssCodecs.Contains(stream.Codec);

        string extension;
        if (isBitmap)
        {
            extension = "mks";
        }
        else if (isAss)
        {
            extension = "ass";
        }
        else
        {
            extension = "vtt";
        }

        Dictionary<string, string> tokens = TemplateResolver.SubtitleTokens(
            language,
            variant,
            mediaTitle
        );
        string resolved = TemplateResolver.Resolve(plan.PlaylistNameTemplate, tokens);
        return $"{resolved}.{extension}";
    }

    private static string ResolveVariant(SubtitleStreamInfo stream) =>
        SubtitleClassifier.ResolveVariant(stream);
}

public record SubtitleOutputInfo(
    string OutputPath,
    string FfmpegCodec,
    string Extension,
    string Language,
    string Variant,
    bool IsBitmap,
    int SourceIndex
);
