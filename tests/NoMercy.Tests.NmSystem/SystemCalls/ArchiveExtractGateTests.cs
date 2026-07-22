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

using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.NmSystem.SystemCalls;

/// <summary>
/// Pure decision-table tests for <see cref="ArchiveExtractGate"/> — the gate
/// <see cref="Archiving.ExtractArchive"/> consults immediately before shelling
/// out to <c>tar</c>/<c>ZipFile</c>. No filesystem or network involved: every
/// case is a direct assertion on the download/verify/extract decision logic.
/// </summary>
public class ArchiveExtractGateTests
{
    [Fact]
    public void CanProceed_FileMissing_ReturnsFalse()
    {
        bool result = ArchiveExtractGate.CanProceed(fileExists: false, actualSizeBytes: 0);

        Assert.False(condition: result, userMessage: "a missing archive must never be handed to the extractor");
    }

    [Fact]
    public void CanProceed_FileMissingButSizeReportedNonZero_ReturnsFalse()
    {
        // Defends against a caller passing a stale cached size for a path that no
        // longer exists — existence is authoritative, size alone is not enough.
        bool result = ArchiveExtractGate.CanProceed(
            fileExists: false,
            actualSizeBytes: 224_525_275
        );

        Assert.False(condition: result);
    }

    [Fact]
    public void CanProceed_ZeroByteFile_ReturnsFalse()
    {
        // The signature of an aborted, partial, or not-yet-flushed download.
        bool result = ArchiveExtractGate.CanProceed(fileExists: true, actualSizeBytes: 0);

        Assert.False(condition: result, userMessage: "a zero-byte file is never a valid archive");
    }

    [Fact]
    public void CanProceed_ExistingNonEmptyFile_ReturnsTrue()
    {
        bool result = ArchiveExtractGate.CanProceed(fileExists: true, actualSizeBytes: 224_525_275);

        Assert.True(condition: result, userMessage: "a fully-downloaded, verified archive must be allowed to extract");
    }

    [Fact]
    public void CanProceed_ExistingOneByteFile_ReturnsTrue()
    {
        // The gate only rejects the impossible (missing/empty) case; it is not a
        // substitute for the caller's own SHA-256/size verification against the
        // release manifest — that happens earlier in the download pipeline.
        bool result = ArchiveExtractGate.CanProceed(fileExists: true, actualSizeBytes: 1);

        Assert.True(condition: result);
    }
}
