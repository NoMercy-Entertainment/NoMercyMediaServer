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

namespace NoMercy.Encoder.Execution;

/// <summary>Suspends/resumes processes via SIGSTOP/SIGCONT (kill) on Unix.</summary>
public sealed class UnixProcessSuspender : IProcessSuspender
{
    public void Suspend(int processId)
    {
        using Process? proc = Process.Start(fileName: "kill", arguments: ["-STOP", processId.ToString()]);
        proc?.WaitForExit(milliseconds: 5000);
    }

    public void Resume(int processId)
    {
        using Process? proc = Process.Start(fileName: "kill", arguments: ["-CONT", processId.ToString()]);
        proc?.WaitForExit(milliseconds: 5000);
    }
}
