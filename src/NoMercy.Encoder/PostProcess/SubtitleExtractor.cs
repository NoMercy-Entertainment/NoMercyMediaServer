namespace NoMercy.Encoder.PostProcess;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

public class SubtitleExtractor
{
    public FfmpegCommand[] BuildExtractionCommands(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        IReadOnlyList<SubtitleStreamInfo> streams,
        SubtitleOutputPlan[] subtitlePlans
    )
    {
        List<FfmpegCommand> commands = [];

        for (int i = 0; i < subtitlePlans.Length; i++)
        {
            SubtitleOutputPlan plan = subtitlePlans[i];

            if (plan.Action is not (StreamAction.Extract or StreamAction.Copy))
                continue;

            SubtitleStreamInfo? stream = i < streams.Count ? streams[i] : null;
            string language = stream?.Language ?? plan.Language ?? "und";

            string extension = plan.OutputCodec switch
            {
                SubtitleCodecType.WebVtt => "vtt",
                SubtitleCodecType.Srt => "srt",
                SubtitleCodecType.Ass => "ass",
                _ => "vtt",
            };

            string codec = plan.OutputCodec switch
            {
                SubtitleCodecType.WebVtt => "webvtt",
                SubtitleCodecType.Srt => "srt",
                SubtitleCodecType.Ass => "ass",
                _ => "webvtt",
            };

            string outputFile = Path.Combine(
                outputDirectory,
                $"subtitles_{language}_{extension}.{extension}"
            );

            FfmpegCommand cmd = new FfmpegCommandBuilder()
                .WithGlobalOptions(new GlobalOptions(ProgressPipe: false, Overwrite: true))
                .AddInput(new InputOptions(inputPath))
                .AddOutput(
                    new OutputOptions(
                        FilePath: outputFile,
                        SubtitleCodec: codec,
                        MapStreams: [$"0:s:{i}"]
                    )
                )
                .Build(ffmpegPath);

            commands.Add(cmd);
        }

        return commands.ToArray();
    }

    public static string ResolveOutputFilename(SubtitleOutputPlan plan, string? streamLanguage)
    {
        string language = streamLanguage ?? plan.Language ?? "und";
        string extension = plan.OutputCodec switch
        {
            SubtitleCodecType.WebVtt => "vtt",
            SubtitleCodecType.Srt => "srt",
            SubtitleCodecType.Ass => "ass",
            _ => "vtt",
        };

        return $"subtitles_{language}_{extension}.{extension}";
    }
}
