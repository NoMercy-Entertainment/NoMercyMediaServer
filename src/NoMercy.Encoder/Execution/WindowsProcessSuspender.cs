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
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NoMercy.Encoder.Execution;

/// <summary>Suspends/resumes processes via ntdll NtSuspendProcess/NtResumeProcess on Windows.</summary>
[SupportedOSPlatform(platformName: "windows")]
public sealed class WindowsProcessSuspender : IProcessSuspender
{
    public void Suspend(int processId)
    {
        // Process is kept alive (via `using`) for as long as its Handle is in
        // use: Process.Handle used to be read from a Process that then went
        // out of scope and became GC-eligible immediately after this method
        // returned. Once collected/finalized the underlying OS handle closes,
        // and Windows can reuse that same handle value for something
        // unrelated — a later NtSuspendProcess/NtResumeProcess call against
        // the stale value could then act on the wrong process entirely.
        using Process? process = TryGetProcess(pid: processId);
        if (process is null)
        {
            return;
        }

        NtSuspendProcess(processHandle: process.Handle);
        // No manual NtClose here — Process.Dispose() (via `using`) owns
        // closing the handle it lazily opened via .Handle above. Closing it
        // here too would double-close the same OS handle value.
    }

    public void Resume(int processId)
    {
        using Process? process = TryGetProcess(pid: processId);
        if (process is null)
        {
            return;
        }

        NtResumeProcess(processHandle: process.Handle);
    }

    private static Process? TryGetProcess(int pid)
    {
        try
        {
            return Process.GetProcessById(processId: pid);
        }
        catch (Exception)
        {
            // Process gone or access denied — callers treat null as a no-op.
            return null;
        }
    }

    [DllImport(dllName: "ntdll.dll")]
    private static extern uint NtSuspendProcess(nint processHandle);

    [DllImport(dllName: "ntdll.dll")]
    private static extern uint NtResumeProcess(nint processHandle);
}
