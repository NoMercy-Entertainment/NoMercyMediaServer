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

namespace NoMercy.Storage;

/// <summary>
/// Process-wide paths the storage facade writes to. Defaults to the OS temp
/// directory so the storage layer is usable without app-level wiring (tests,
/// CLI tools), but the host (`NoMercy.Service` startup) overrides both with
/// `AppFiles.TempPath` and `AppFiles.TranscodePath` so encoder + lease files
/// land inside the NoMercy data directory instead of scattering on the user's
/// machine.
///
/// Static seam rather than DI because <see cref="Remote.RemoteStorage"/> and
/// the orchestrator's temp-dir logic run from places that don't carry an
/// IServiceProvider — and adding a project ref to NmSystem would invert the
/// dependency direction.
/// </summary>
public static class StoragePaths
{
    /// <summary>
    /// Where AcquireLocalPath stages remote-source copies. Default: OS temp.
    /// Should be set to <c>AppFiles.TempPath</c> at host startup.
    /// </summary>
    public static string TempRoot { get; set; } = Path.GetTempPath();

    /// <summary>
    /// Where the encode orchestrator writes ffmpeg output before uploading
    /// to a remote destination. Default: OS temp. Should be set to
    /// <c>AppFiles.TranscodePath</c> at host startup.
    /// </summary>
    public static string TranscodeRoot { get; set; } = Path.GetTempPath();
}
