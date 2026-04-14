namespace NoMercy.Encoder.Infrastructure;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default
    );

    Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Runs a process with a kill signal. When <paramref name="killSignal"/> fires,
    /// the process is terminated and the result is returned normally (not as an error).
    /// Use this for long-running processes like FFmpeg that may hang after output is complete.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        Action<string>? onStdOut,
        Action<string>? onStdErr,
        string? workingDirectory,
        CancellationToken cancellationToken,
        CancellationToken killSignal,
        Action<int>? onProcessStarted = null
    );
}
