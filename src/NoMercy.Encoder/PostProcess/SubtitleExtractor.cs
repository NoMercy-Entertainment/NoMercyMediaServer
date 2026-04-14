namespace NoMercy.Encoder.PostProcess;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Subtitles;

public class SubtitleExtractor
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
    public static SubtitleOutputInfo ResolveOutput(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string outputDirectory,
        string mediaTitle
    )
    {
        string language = stream.Language ?? plan.Language ?? "und";
        string variant =
            stream.IsForced ? "sign"
            : stream.IsDefault ? "full"
            : "sdh";
        bool isBitmap = SubtitleClassifier.IsBitmapBased(stream.Codec);
        bool isAss = AssCodecs.Contains(stream.Codec);

        string extension;
        string ffmpegCodec;

        if (isBitmap)
        {
            // Bitmap subs extracted as MKS (Matroska subtitle container).
            // FFmpeg can't write .sub+.idx from MKV in a single command.
            extension = "mks";
            ffmpegCodec = "copy";
        }
        else if (isAss)
        {
            extension = "ass";
            ffmpegCodec = "ass";
        }
        else
        {
            // All other text formats → WebVTT
            extension = "vtt";
            ffmpegCodec = "webvtt";
        }

        Dictionary<string, string> tokens = TemplateResolver.SubtitleTokens(
            language,
            variant,
            mediaTitle
        );
        string resolved = TemplateResolver.Resolve(plan.PlaylistNameTemplate, tokens);
        // Relative path — FFmpeg CWD is set to the output directory.
        string outputPath = $"{resolved}.{extension}";

        return new SubtitleOutputInfo(
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
    public static string ResolvePlaylistUri(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string mediaTitle
    )
    {
        string language = stream.Language ?? plan.Language ?? "und";
        string variant =
            stream.IsForced ? "sign"
            : stream.IsDefault ? "full"
            : "sdh";
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
