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

namespace NoMercy.NmSystem.SystemCalls;

/// <summary>
/// Pure decision gate for whether a downloaded archive is safe to hand to the
/// extractor (<c>tar</c> or <see cref="System.IO.Compression.ZipFile"/>).
/// </summary>
/// <remarks>
/// Deliberately has no I/O of its own — <see cref="Archiving"/> supplies the
/// observed file state so this decision is unit-testable without touching disk
/// or the network. A missing file, a zero-byte file, or a download that never
/// completed must never reach the extractor: shelling out to <c>tar</c> against
/// an absent path produces a cryptic "No such file or directory" from a child
/// process instead of a clear, actionable error at the call site.
/// </remarks>
public static class ArchiveExtractGate
{
    /// <summary>
    /// Returns <c>true</c> only when the archive actually exists on disk and is
    /// non-empty. Zero bytes is never a valid archive — a 0-byte file is the
    /// signature of an aborted or not-yet-flushed download, never a real one.
    /// </summary>
    public static bool CanProceed(bool fileExists, long actualSizeBytes) =>
        fileExists && actualSizeBytes > 0;
}
