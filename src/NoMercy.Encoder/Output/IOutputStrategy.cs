namespace NoMercy.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;

public interface IOutputStrategy
{
    OutputFormat Format { get; }

    void ConfigureOutput(FfmpegCommandBuilder builder, OutputPlan plan, string outputDirectory);

    Task FinalizeAsync(string outputDirectory, OutputPlan plan, CancellationToken ct);

    string[] GetOutputSubdirectories(OutputPlan plan);
}
