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
[SupportedOSPlatform("windows")]
public sealed class WindowsProcessSuspender : IProcessSuspender
{
    public void Suspend(int processId)
    {
        nint handle = NtOpenProcess(processId);
        if (handle != nint.Zero)
        {
            NtSuspendProcess(handle);
            NtClose(handle);
        }
    }

    public void Resume(int processId)
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
            // Process gone or access denied — callers treat Zero as a no-op.
            return nint.Zero;
        }
    }

    [DllImport("ntdll.dll")]
    private static extern uint NtSuspendProcess(nint processHandle);

    [DllImport("ntdll.dll")]
    private static extern uint NtResumeProcess(nint processHandle);

    [DllImport("ntdll.dll")]
    private static extern uint NtClose(nint handle);
}
