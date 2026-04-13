namespace NoMercy.Encoder.PostProcess;

using System.Text;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;

public class ThumbnailGenerator
{
    public FfmpegCommand BuildCaptureCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        ThumbnailOutputPlan plan,
        TimeSpan duration
    )
    {
        string thumbDir = Path.Combine(outputDirectory, $"thumbs_{plan.Width}");

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new GlobalOptions(ProgressPipe: false, Overwrite: true))
            .AddInput(new InputOptions(inputPath))
            .AddOutput(
                new OutputOptions(
                    FilePath: Path.Combine(thumbDir, "thumb_%04d.jpg"),
                    MapStreams: ["0:v:0"],
                    ExtraFlags: new Dictionary<string, string>
                    {
                        ["-vf"] = $"fps=1/{plan.IntervalSeconds},scale={plan.Width}:-2",
                        ["-q:v"] = "5",
                    }
                )
            )
            .Build(ffmpegPath);

        return cmd;
    }

    public FfmpegCommand BuildSpriteCommand(
        string ffmpegPath,
        string outputDirectory,
        ThumbnailOutputPlan plan,
        int imageCount
    )
    {
        string thumbDir = Path.Combine(outputDirectory, $"thumbs_{plan.Width}");
        (int gridWidth, int gridHeight) = ComputeGrid(imageCount);
        string spriteFile = Path.Combine(outputDirectory, $"thumbs_{plan.Width}.webp");

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new GlobalOptions(ProgressPipe: false, Overwrite: true))
            .AddInput(new InputOptions(Path.Combine(thumbDir, "thumb_%04d.jpg")))
            .AddOutput(
                new OutputOptions(
                    FilePath: spriteFile,
                    ExtraFlags: new Dictionary<string, string>
                    {
                        ["-filter_complex"] = $"tile={gridWidth}x{gridHeight}",
                    }
                )
            )
            .Build(ffmpegPath);

        return cmd;
    }

    public async Task WriteVttCueFileAsync(
        string outputDirectory,
        ThumbnailOutputPlan plan,
        int imageCount,
        TimeSpan duration,
        CancellationToken ct
    )
    {
        (int gridWidth, _) = ComputeGrid(imageCount);
        string spriteFilename = $"thumbs_{plan.Width}.webp";
        string vttFile = Path.Combine(outputDirectory, $"thumbs_{plan.Width}.vtt");

        // 16:9 proportional height
        int thumbHeight = plan.Width * 9 / 16;

        StringBuilder sb = new();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        for (int i = 0; i < imageCount; i++)
        {
            TimeSpan start = TimeSpan.FromSeconds(i * plan.IntervalSeconds);
            TimeSpan end = TimeSpan.FromSeconds(
                Math.Min((i + 1) * plan.IntervalSeconds, duration.TotalSeconds)
            );

            int col = i % gridWidth;
            int row = i / gridWidth;
            int x = col * plan.Width;
            int y = row * thumbHeight;

            sb.AppendLine($"{i + 1}");
            sb.AppendLine($"{FormatVttTime(start)} --> {FormatVttTime(end)}");
            sb.AppendLine($"{spriteFilename}#xywh={x},{y},{plan.Width},{thumbHeight}");
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(vttFile, sb.ToString(), ct);
    }

    public void CleanupIndividualThumbnails(string outputDirectory, ThumbnailOutputPlan plan)
    {
        string thumbDir = Path.Combine(outputDirectory, $"thumbs_{plan.Width}");

        if (Directory.Exists(thumbDir))
            Directory.Delete(thumbDir, true);
    }

    public static (int Width, int Height) ComputeGrid(int imageCount)
    {
        int gridWidth = (int)Math.Ceiling(Math.Sqrt(imageCount));
        int gridHeight = (int)Math.Ceiling((double)imageCount / gridWidth);

        return (gridWidth, gridHeight);
    }

    private static string FormatVttTime(TimeSpan ts) =>
        $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
}
