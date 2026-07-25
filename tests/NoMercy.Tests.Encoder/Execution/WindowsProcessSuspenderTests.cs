// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.Versioning;
using NoMercy.Encoder.Execution;

namespace NoMercy.Tests.Encoder.Execution;

/// <summary>
/// WindowsProcessSuspender used to read Process.Handle from a Process object
/// that immediately went out of scope — once GC collected/finalized it, the
/// underlying OS handle closed and Windows could reassign that handle value
/// to something unrelated before NtSuspendProcess/NtResumeProcess/NtClose ran
/// against it. The fix keeps the Process alive (via `using`) for the whole
/// suspend/resume call and lets Process.Dispose() own closing the handle
/// exactly once, instead of also calling NtClose manually.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsProcessSuspenderTests
{
    [SkippableFact]
    public void Suspend_UnknownProcessId_DoesNotThrow()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "WindowsProcessSuspender is Windows-only");

        WindowsProcessSuspender suspender = new();

        Action act = () => suspender.Suspend(int.MaxValue);

        act.Should().NotThrow();
    }

    [SkippableFact]
    public void Resume_UnknownProcessId_DoesNotThrow()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "WindowsProcessSuspender is Windows-only");

        WindowsProcessSuspender suspender = new();

        Action act = () => suspender.Resume(int.MaxValue);

        act.Should().NotThrow();
    }

    [SkippableFact]
    public void Suspend_Resume_RealProcess_UnderConcurrentGcPressure_DoesNotThrowAndProcessSurvives()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "WindowsProcessSuspender is Windows-only");

        // ping, not timeout.exe — timeout.exe checks for an interactive
        // console and exits immediately under redirected I/O (CreateNoWindow),
        // which would make the process exit on its own mid-test regardless of
        // suspend/resume.
        using Process target = Process.Start(
            new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping -n 31 127.0.0.1 >nul",
                CreateNoWindow = true,
                UseShellExecute = false,
            }
        )!;

        try
        {
            WindowsProcessSuspender suspender = new();
            using CancellationTokenSource stopGcPressure = new();

            // Aggressive concurrent GC is exactly the condition the old bug
            // needed: collect the Process wrapper between NtOpenProcess
            // returning its handle and the Nt*Process call using it.
            Task gcPressureTask = Task.Run(() =>
            {
                while (!stopGcPressure.IsCancellationRequested)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            });

            Action act = () =>
            {
                for (int i = 0; i < 25; i++)
                {
                    suspender.Suspend(target.Id);
                    suspender.Resume(target.Id);
                }
            };

            act.Should().NotThrow();

            stopGcPressure.Cancel();
            gcPressureTask.Wait(TimeSpan.FromSeconds(5));

            target.Refresh();
            target
                .HasExited.Should()
                .BeFalse("suspend/resume cycling must not kill or corrupt the target process");
        }
        finally
        {
            try
            {
                if (!target.HasExited)
                    target.Kill();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
