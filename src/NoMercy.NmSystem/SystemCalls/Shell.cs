using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using NoMercy.NmSystem.Dto;
using System.Runtime.InteropServices;

namespace NoMercy.NmSystem.SystemCalls;

public static class Shell
{
    private static readonly ExecutorRegistry Registry = new();
    
    public static ExecResult ExecSync(string executable, string arguments, ExecOptions? options = null, CancellationTokenSource? cts = null) =>
        ExecAsync(executable, arguments, options, cts).GetAwaiter().GetResult();

    public static async Task<ExecResult> ExecAsync(string executable, string arguments, ExecOptions? options = null, CancellationTokenSource? cts = null)
    {
        options ??= new();
        using Process process = CreateProcess(executable, arguments, options);

        (StringBuilder outputBuilder, StringBuilder errorBuilder) = AttachOutputHandlers(process, options);

        try
        {
            process.Start();
            BeginReadingOutput(process, options);
            await process.WaitForExitAsync(cts.Token);

            return await BuildExecResult(process, outputBuilder, errorBuilder, options);
        }
        catch (Exception ex)
        {
            return new()
            {
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = $"Error executing command: {ex.Message}"
            };
        }
    }

    public static async Task<string> ExecStdOutAsync(string executable, string arguments, ExecOptions? options = null, CancellationTokenSource? cts = null) =>
        (await ExecAsync(executable, arguments, options, cts)).StandardOutput;

    public static async Task<string> ExecStdErrAsync(string executable, string arguments, ExecOptions? options = null, CancellationTokenSource? cts = null)
    {
        options ??= new() { CaptureStdErr = true, CaptureStdOut = false };
        return (await ExecAsync(executable, arguments, options, cts)).StandardError;
    }

    public static string ExecCommand(string command)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return string.IsNullOrEmpty(output) ? "Unknown" : output;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running command: {ex.Message}");
        }

        return "Unknown";
    }
    
    public static string ExecStdOutSync(string executable, string arguments, ExecOptions? options = null, CancellationTokenSource? cts = null) =>
        ExecSync(executable, arguments, options, cts).StandardOutput;

    public static string ExecStdErrSync(string executable, string arguments, ExecOptions? options = null, CancellationTokenSource? cts = null)
    {
        options ??= new() { CaptureStdErr = true, CaptureStdOut = false };
        return ExecSync(executable, arguments, options, cts).StandardError;
    }
    
    public static ExecutorHandle StartAndRegister(
        string executable,
        string arguments,
        out Task<ExecResult> runningTask,
        ExecOptions? options = null,
        Action<string>? stdoutCallback = null,
        Action<string>? stderrCallback = null,
        CancellationToken cancellationToken = default,
        string? jobId = null
    )
    {
        options ??= new();
        ValidateWorkingDirectory(options.WorkingDirectory);

        Process process = CreateProcess(executable, arguments, options);
        (StringBuilder stdoutBuilder, StringBuilder stderrBuilder, ConcurrentQueue<Exception> callbackExceptions) = AttachCallbackHandlers(
            process, options, stdoutCallback, stderrCallback);

        ExecutorHandle handle = CreateHandle(executable, arguments, jobId, process);

        if (!process.Start())
            throw new InvalidOperationException("Failed to start process");

        BeginReadingOutput(process, options);
        Registry.Register(handle);

        handle.RunningTask = MonitorProcessAsync(
            process, stdoutBuilder, stderrBuilder, callbackExceptions, 
            stdoutCallback, options, handle, cancellationToken);

        runningTask = handle.RunningTask;
        return handle;
    }

    public static bool Pause(Guid executorId) =>
        TryChangeProcessState(executorId, TrySuspend, ExecutorState.Paused);

    public static bool Resume(Guid executorId) =>
        TryChangeProcessState(executorId, TryResume, ExecutorState.Resuming);

    // Helper methods
    
    private static Process CreateProcess(string executable, string arguments, ExecOptions options)
    {
        Process process = new()
        {
            StartInfo =
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = options.WorkingDirectory ?? string.Empty,
                UseShellExecute = options.UseShellExecute,
                CreateNoWindow = options.CreateNoWindow,
                RedirectStandardInput = options.RedirectInput,
                RedirectStandardOutput = options.CaptureStdOut,
                RedirectStandardError = options.ShouldRedirectStdErr()
            }
        };

        foreach ((string key, string value) in options.EnvironmentVariables)
            process.StartInfo.EnvironmentVariables[key] = value;

        return process;
    }

    private static (StringBuilder output, StringBuilder error) AttachOutputHandlers(
        Process process, ExecOptions options)
    {
        StringBuilder outputBuilder = new();
        StringBuilder errorBuilder = new();

        if (options.CaptureStdOut)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
        }

        if (options.ShouldRedirectStdErr())
        {
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };
        }

        return (outputBuilder, errorBuilder);
    }

    private static (StringBuilder stdout, StringBuilder stderr, ConcurrentQueue<Exception> exceptions) 
        AttachCallbackHandlers(
            Process process, 
            ExecOptions options,
            Action<string>? stdoutCallback,
            Action<string>? stderrCallback)
    {
        StringBuilder stdoutBuilder = new();
        StringBuilder stderrBuilder = new();
        ConcurrentQueue<Exception> callbackExceptions = new();

        if (options.CaptureStdOut)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                TryInvokeCallback(stdoutCallback, e.Data, callbackExceptions, process);
                stdoutBuilder.AppendLine(e.Data);
            };
        }

        if (options.ShouldRedirectStdErr())
        {
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                TryInvokeCallback(stderrCallback, e.Data, callbackExceptions, process);
                stderrBuilder.AppendLine(e.Data);
            };
        }

        return (stdoutBuilder, stderrBuilder, callbackExceptions);
    }

    private static void TryInvokeCallback(
        Action<string>? callback, 
        string data, 
        ConcurrentQueue<Exception> exceptions, 
        Process process)
    {
        try 
        { 
            callback?.Invoke(data); 
        }
        catch (Exception ex) 
        { 
            exceptions.Enqueue(ex); 
            TryKillProcess(process); 
        }
    }

    private static void BeginReadingOutput(Process process, ExecOptions options)
    {
        if (options.CaptureStdOut) process.BeginOutputReadLine();
        if (options.ShouldRedirectStdErr()) process.BeginErrorReadLine();
    }

    private static async Task<ExecResult> BuildExecResult(
        Process process, 
        StringBuilder outputBuilder, 
        StringBuilder errorBuilder, 
        ExecOptions options)
    {
        string stdOutput = outputBuilder.ToString().Trim();
        string stdError = errorBuilder.ToString().Trim();

        if (options.MergeStdErrToOut)
            stdOutput += await process.StandardError.ReadToEndAsync();

        return new()
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdOutput,
            StandardError = stdError
        };
    }

    private static ExecutorHandle CreateHandle(string executable, string arguments, string? jobId, Process process)
    {
        return new()
        {
            Executable = executable,
            Arguments = arguments,
            JobId = jobId,
            State = ExecutorState.Running,
            Process = process,
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    private static bool TryChangeProcessState(
        Guid executorId, 
        Func<Process?, bool> stateChanger, 
        ExecutorState newState)
    {
        if (!Registry.TryGet(executorId, out ExecutorHandle? handle)) return false;
        if (handle?.Process == null || handle.Process.HasExited) return false;

        bool success = stateChanger(handle.Process);
        if (success) handle.State = newState;
        return success;
    }

    private static void ValidateWorkingDirectory(string? workingDirectory)
    {
        if (workingDirectory != null &&
            !string.IsNullOrWhiteSpace(workingDirectory) &&
            !Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The specified working directory does not exist: {workingDirectory}");
        }
    }

    private static async Task<ExecResult> MonitorProcessAsync(
        Process process,
        StringBuilder stdoutBuilder,
        StringBuilder stderrBuilder,
        ConcurrentQueue<Exception> callbackExceptions,
        Action<string>? stdoutCallback,
        ExecOptions options,
        ExecutorHandle handle,
        CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, handle.Cancellation.Token);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            await HandleMergedStdErr(process, options, stderrBuilder, stdoutBuilder, 
                stdoutCallback, callbackExceptions);

            if (!callbackExceptions.IsEmpty)
            {
                throw new AggregateException(
                    "One or more errors occurred in output callbacks.",
                    callbackExceptions.ToArray());
            }

            CompleteHandle(handle);

            return new()
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdoutBuilder.ToString().Trim(),
                StandardError = stderrBuilder.ToString().Trim()
            };
        }
        catch (OperationCanceledException)
        {
            handle.State = ExecutorState.Cancelled;
            Registry.TryRemove(handle.Id, out _);
            throw;
        }
        catch (Exception ex)
        {
            handle.State = ExecutorState.Failed;
            Registry.TryRemove(handle.Id, out _);
            throw new InvalidOperationException(
                $"Execution failed for handle {handle.Id}: {ex.Message}", ex);
        }
    }

    private static async Task HandleMergedStdErr(
        Process process,
        ExecOptions options,
        StringBuilder stderrBuilder,
        StringBuilder stdoutBuilder,
        Action<string>? stdoutCallback,
        ConcurrentQueue<Exception> callbackExceptions)
    {
        if (options is not { MergeStdErrToOut: true, CaptureStdErr: true }) return;

        try
        {
            string remainingErr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(remainingErr)) return;

            stderrBuilder.AppendLine(remainingErr);
            foreach (string line in SplitLines(remainingErr))
            {
                TryInvokeCallback(stdoutCallback, line, callbackExceptions, process);
                stdoutBuilder.AppendLine(line);
            }
        }
        catch (Exception ex)
        {
            callbackExceptions.Enqueue(ex);
        }
    }

    private static void CompleteHandle(ExecutorHandle handle)
    {
        handle.State = ExecutorState.Completed;
        handle.CompletedAt = DateTimeOffset.UtcNow;
        Registry.TryRemove(handle.Id, out _);
    }

    private static IEnumerable<string> SplitLines(string s)
    {
        using StringReader reader = new(s);
        string? line;
        while ((line = reader.ReadLine()) != null)
            yield return line;
    }
    
    // Process control
    
    private const int SIGSTOP = 19;
    private const int SIGCONT = 18;
    
    private static bool TrySuspend(Process? process) =>
        ChangeProcessThreadState(process, SIGSTOP, ProcessHelper.SuspendThread);

    private static bool TryResume(Process? process) =>
        ChangeProcessThreadState(process, SIGCONT, ProcessHelper.ResumeThread);

    private static bool ChangeProcessThreadState(Process? process, int signal, Action<IntPtr> windowsAction)
    {
        if (process is null || process.Id <= 0) return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ProcessHelper.SendSignal(process.Id, signal);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ModifyWindowsProcessThreads(process, windowsAction);

        return false;
    }

    private static bool ModifyWindowsProcessThreads(Process process, Action<IntPtr> threadAction)
    {
        const int ThreadSuspendResume = 0x0002;
        
        try
        {
            foreach (ProcessThread thread in process.Threads)
            {
                IntPtr threadHandle = ProcessHelper.OpenThread(ThreadSuspendResume, false, (uint)thread.Id);
                if (threadHandle == IntPtr.Zero) continue;
                
                threadAction(threadHandle);
                ProcessHelper.CloseHandle(threadHandle);
            }
            return true;
        }
        catch (Exception e)
        {
            Logger.App($"Failed to modify process {process.Id}: {e.Message}");
            return false;
        }
    }

    private static void TryKillProcess(Process p)
    {
        try
        {
            if (!p.HasExited)
                p.Kill(entireProcessTree: true);
        }
        catch (Exception killEx)
        {
            Debug.WriteLine($"Failed to kill process after callback error: {killEx.Message}");
        }
    }
    
    public static class ProcessHelper
    {
        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);

        internal static bool SendSignal(int pid, int sig)
        {
            try { return kill(pid, sig) == 0; }
            catch { return false; }
        }

        [DllImport("kernel32.dll")]
        internal static extern IntPtr OpenThread(int dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);
    
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(CtrlTypes dwCtrlEvent, uint dwProcessGroupId);

        private enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0
        }

        public static void SendCtrlC(Process process)
        {
            if (!AttachConsole((uint)process.Id)) return;
            
            GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0);
            FreeConsole();
        }
    }
}

// Extension methods for ExecOptions
internal static class ExecOptionsExtensions
{
    internal static bool ShouldRedirectStdErr(this ExecOptions options) =>
        options is { CaptureStdErr: true, MergeStdErrToOut: false };
}