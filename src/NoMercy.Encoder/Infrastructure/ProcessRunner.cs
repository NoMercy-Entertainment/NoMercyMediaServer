namespace NoMercy.Encoder.Infrastructure;

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

    public Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        return RunCoreAsync(
            executable,
            arguments,
            onStdOut,
            onStdErr,
            workingDirectory,
            cancellationToken,
            killSignal: default
        );
    }

    public Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        Action<string>? onStdOut,
        Action<string>? onStdErr,
        string? workingDirectory,
        CancellationToken cancellationToken,
        CancellationToken killSignal
    )
    {
        return RunCoreAsync(
            executable,
            arguments,
            onStdOut,
            onStdErr,
            workingDirectory,
            cancellationToken,
            killSignal
        );
    }

    private async Task<ProcessResult> RunCoreAsync(
        string executable,
        string[] arguments,
        Action<string>? onStdOut,
        Action<string>? onStdErr,
        string? workingDirectory,
        CancellationToken cancellationToken,
        CancellationToken killSignal
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

        // When killSignal fires, terminate the process tree.
        // This is NOT an error — the caller decided output is complete.
        bool killedBySignal = false;
        CancellationTokenRegistration killRegistration = default;
        if (killSignal.CanBeCanceled)
        {
            killRegistration = killSignal.Register(() =>
            {
                killedBySignal = true;
                try
                {
                    if (!process.HasExited)
                    {
                        logger.LogDebug(
                            "Kill signal received — terminating process: {Executable}",
                            executable
                        );
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException) { }
            });
        }

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (killedBySignal && !cancellationToken.IsCancellationRequested)
        {
            // Process was killed by our kill signal, not by user cancellation.
            // Wait briefly for the kill to finalize.
            try
            {
                using CancellationTokenSource finalizeCts = new(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(finalizeCts.Token);
            }
            catch
            { /* best effort */
            }
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
        finally
        {
            await killRegistration.DisposeAsync();
        }

        stopwatch.Stop();

        int exitCode = killedBySignal ? 0 : process.ExitCode;

        ProcessResult result = new(
            ExitCode: exitCode,
            StdOut: stdOutBuilder.ToString().TrimEnd(),
            StdErr: stdErrBuilder.ToString().TrimEnd(),
            Duration: stopwatch.Elapsed
        );

        logger.LogDebug(
            "Process exited: {Executable} ExitCode={ExitCode} Duration={Duration}ms{KillNote}",
            executable,
            result.ExitCode,
            result.Duration.TotalMilliseconds,
            killedBySignal ? " (killed by signal)" : ""
        );

        return result;
    }
}
