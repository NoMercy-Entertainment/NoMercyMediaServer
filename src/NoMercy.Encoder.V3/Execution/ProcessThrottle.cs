namespace NoMercy.Encoder.V3.Execution;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

public class ProcessThrottle(ILogger<ProcessThrottle> logger)
{
    private readonly HashSet<int> _suspendedPids = [];

    public void Suspend(int processId)
    {
        if (_suspendedPids.Contains(processId))
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SuspendWindows(processId);
        }
        else
        {
            SuspendUnix(processId);
        }

        _suspendedPids.Add(processId);
        logger.LogDebug("Suspended process {Pid}", processId);
    }

    public void Resume(int processId)
    {
        if (!_suspendedPids.Contains(processId))
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ResumeWindows(processId);
        }
        else
        {
            ResumeUnix(processId);
        }

        _suspendedPids.Remove(processId);
        logger.LogDebug("Resumed process {Pid}", processId);
    }

    public bool IsSuspended(int processId) => _suspendedPids.Contains(processId);

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
        catch
        {
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
