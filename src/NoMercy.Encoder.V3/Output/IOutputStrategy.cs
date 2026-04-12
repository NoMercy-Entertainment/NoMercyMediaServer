namespace NoMercy.Encoder.V3.Output;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;

public interface IOutputStrategy
{
    OutputFormat Format { get; }

    void ConfigureOutput(FfmpegCommandBuilder builder, OutputPlan plan, string outputDirectory);

    Task FinalizeAsync(string outputDirectory, OutputPlan plan, CancellationToken ct);

    string[] GetOutputSubdirectories(OutputPlan plan);
}
