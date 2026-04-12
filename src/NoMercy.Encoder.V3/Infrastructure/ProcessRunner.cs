namespace NoMercy.Encoder.V3.Infrastructure;

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

public class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    public Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(executable, arguments, null, null, workingDirectory, cancellationToken);
    }

    public async Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        StringBuilder stdOutBuilder = new();
        StringBuilder stdErrBuilder = new();

        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
        };

        foreach (string arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        logger.LogDebug(
            "Starting process: {Executable} {Arguments}",
            executable,
            string.Join(" ", arguments)
        );

        using Process process = new() { StartInfo = startInfo };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            stdOutBuilder.AppendLine(e.Data);
            onStdOut?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            stdErrBuilder.AppendLine(e.Data);
            onStdErr?.Invoke(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }

            throw;
        }

        stopwatch.Stop();

        ProcessResult result = new(
            ExitCode: process.ExitCode,
            StdOut: stdOutBuilder.ToString().TrimEnd(),
            StdErr: stdErrBuilder.ToString().TrimEnd(),
            Duration: stopwatch.Elapsed
        );

        logger.LogDebug(
            "Process exited: {Executable} ExitCode={ExitCode} Duration={Duration}ms",
            executable,
            result.ExitCode,
            result.Duration.TotalMilliseconds
        );

        return result;
    }
}
