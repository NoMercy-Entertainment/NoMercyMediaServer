namespace NoMercy.Encoder.Infrastructure;

public record ProcessResult(
    int ExitCode,
    string StdOut,
    string StdErr,
    TimeSpan Duration,
    int ProcessId = 0
)
{
    public bool IsSuccess => ExitCode == 0;
}
