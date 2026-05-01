using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NoMercy.Encoder.Infrastructure;

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
        CancellationToken killSignal,
        Action<int>? onProcessStarted = null
    )
    {
        return RunCoreAsync(
            executable,
            arguments,
            onStdOut,
            onStdErr,
            workingDirectory,
            cancellationToken,
            killSignal,
            onProcessStarted
        );
    }

    public Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        IReadOnlyDictionary<string, string>? extraEnv,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        return RunCoreAsync(
            executable,
            arguments,
            onStdOut: null,
            onStdErr: null,
            workingDirectory,
            cancellationToken,
            killSignal: default,
            onProcessStarted: null,
            extraEnv: extraEnv
        );
    }

    private async Task<ProcessResult> RunCoreAsync(
        string executable,
        string[] arguments,
        Action<string>? onStdOut,
        Action<string>? onStdErr,
        string? workingDirectory,
        CancellationToken cancellationToken,
        CancellationToken killSignal,
        Action<int>? onProcessStarted = null,
        IReadOnlyDictionary<string, string>? extraEnv = null
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
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        if (extraEnv is { Count: > 0 })
        {
            foreach (KeyValuePair<string, string> kv in extraEnv)
                startInfo.Environment[kv.Key] = kv.Value;
        }

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
        onProcessStarted?.Invoke(process.Id);
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
            // User-initiated cancellation: attempt a graceful shutdown first,
            // then force-kill if FFmpeg does not exit within the grace period.
            await KillGracefullyAsync(process, executable, logger);
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
            Duration: stopwatch.Elapsed,
            ProcessId: process.Id
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

    /// <summary>
    /// Attempts a graceful shutdown before resorting to force-kill.
    /// On Windows: <c>CloseMainWindow()</c> sends WM_CLOSE (analogous to
    /// Ctrl+C for console apps); if the process does not exit within 5 s,
    /// falls back to <c>Kill(entireProcessTree: true)</c>.
    /// On Linux/macOS: <c>Kill(false)</c> sends SIGTERM; same 5 s grace,
    /// then <c>Kill(true)</c> (SIGKILL).
    /// </summary>
    private static async Task KillGracefullyAsync(
        Process process,
        string executable,
        ILogger logger
    )
    {
        if (process.HasExited)
            return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // CloseMainWindow sends WM_CLOSE to the console window;
                // FFmpeg treats this like Ctrl+C and flushes output before exiting.
                process.CloseMainWindow();
            }
            else
            {
                // SIGTERM — FFmpeg catches this and exits cleanly.
                process.Kill(entireProcessTree: false);
            }
        }
        catch (InvalidOperationException)
        {
            return; // already exited
        }

        // Wait up to 5 s for graceful exit.
        try
        {
            using CancellationTokenSource graceCts = new(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(graceCts.Token);
            logger.LogDebug(
                "Process exited gracefully after cancel signal: {Executable}",
                executable
            );
            return;
        }
        catch
        {
            // Grace period expired or wait failed — fall through to force-kill.
        }

        // Force-kill the entire process tree.
        try
        {
            if (!process.HasExited)
            {
                logger.LogDebug(
                    "Grace period expired — force-killing process tree: {Executable}",
                    executable
                );
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) { }
    }
}
