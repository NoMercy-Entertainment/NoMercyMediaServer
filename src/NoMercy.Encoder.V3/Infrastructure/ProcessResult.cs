namespace NoMercy.Encoder.V3.Infrastructure;

public record ProcessResult(int ExitCode, string StdOut, string StdErr, TimeSpan Duration)
{
    public bool IsSuccess => ExitCode == 0;
}
