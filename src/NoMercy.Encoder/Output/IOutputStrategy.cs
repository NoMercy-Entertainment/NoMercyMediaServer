using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;

namespace NoMercy.Encoder.Output;

public interface IOutputStrategy
{
    OutputFormat Format { get; }

    void ConfigureOutput(FfmpegCommandBuilder builder, OutputPlan plan, string outputDirectory);

    Task FinalizeAsync(
        string outputDirectory,
        OutputPlan plan,
        string mediaTitle,
        CancellationToken ct
    );

    string[] GetOutputSubdirectories(OutputPlan plan);
}
