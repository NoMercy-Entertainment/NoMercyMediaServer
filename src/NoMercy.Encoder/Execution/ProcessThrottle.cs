using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace NoMercy.Encoder.Execution;

public class ProcessThrottle(ILogger<ProcessThrottle> logger)
{
    // Registered as a singleton in DI and called concurrently from every
    // worker thread that wants to suspend/resume its ffmpeg child. The
    // HashSet must be guarded — without the lock, two threads adding distinct
    // pids at the same time could corrupt internal buckets and a third caller
    // could see a phantom membership result.
    private readonly HashSet<int> _suspendedPids = [];
    private readonly Lock _lock = new();

    public void Suspend(int processId)
    {
        // Lock spans the OS call so a concurrent Resume on the same pid can't
        // interleave between set-add and the actual SIGSTOP, leaving the OS
        // state and the tracked state out of sync. Workers throttle per-pid
        // so contention is negligible.
        lock (_lock)
        {
            if (!_suspendedPids.Add(processId))
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SuspendWindows(processId);
            else
                SuspendUnix(processId);

            logger.LogDebug("Suspended process {Pid}", processId);
        }
    }

    public void Resume(int processId)
    {
        lock (_lock)
        {
            if (!_suspendedPids.Remove(processId))
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                ResumeWindows(processId);
            else
                ResumeUnix(processId);

            logger.LogDebug("Resumed process {Pid}", processId);
        }
    }

    public bool IsSuspended(int processId)
    {
        lock (_lock)
        {
            return _suspendedPids.Contains(processId);
        }
    }

    private static void SuspendUnix(int processId)
    {
        // SIGSTOP
        Process.Start("kill", ["-STOP", processId.ToString()])?.WaitForExit(5000);
    }

    private static void ResumeUnix(int processId)
    {
        // SIGCONT
        Process.Start("kill", ["-CONT", processId.ToString()])?.WaitForExit(5000);
    }

    [SupportedOSPlatform("windows")]
    private static void SuspendWindows(int processId)
    {
        nint handle = NtOpenProcess(processId);
        if (handle != nint.Zero)
        {
            NtSuspendProcess(handle);
            NtClose(handle);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ResumeWindows(int processId)
    {
        nint handle = NtOpenProcess(processId);
        if (handle != nint.Zero)
        {
            NtResumeProcess(handle);
            NtClose(handle);
        }
    }

    private static nint NtOpenProcess(int pid)
    {
        try
        {
            Process process = Process.GetProcessById(pid);
            return process.Handle;
        }
        catch (Exception)
        {
            // Process is already gone or we lack permission — caller checks
            // for IntPtr.Zero. Bare catch promoted to typed Exception so the
            // intent (swallow ALL failures) is explicit.
            return nint.Zero;
        }
    }

    [SupportedOSPlatform("windows")]
    [DllImport("ntdll.dll")]
    private static extern uint NtSuspendProcess(nint processHandle);

    [SupportedOSPlatform("windows")]
    [DllImport("ntdll.dll")]
    private static extern uint NtResumeProcess(nint processHandle);

    [SupportedOSPlatform("windows")]
    [DllImport("ntdll.dll")]
    private static extern uint NtClose(nint handle);
}
