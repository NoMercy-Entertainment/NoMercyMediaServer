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

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="StoragePaths"/> is the static seam the host overrides at startup
/// (<c>AppFiles.TempPath</c> / <c>AppFiles.TranscodePath</c>) so remote-driver
/// staging and encoder output land inside the NoMercy data directory instead
/// of the OS temp folder. These tests demand both the documented default
/// (usable without host wiring) and that the setter actually takes effect —
/// a regression here would silently scatter staged files back into the OS
/// temp directory for every self-hosted install.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StoragePathsTests
{
    [Fact]
    public void TempRoot_defaults_to_os_temp_path_when_not_overridden()
    {
        // Reset defensively in case another test in the run already mutated
        // the static — this is what "default without host wiring" means.
        string original = StoragePaths.TempRoot;
        try
        {
            StoragePaths.TempRoot = Path.GetTempPath();

            StoragePaths
                .TempRoot.Should()
                .Be(
                    Path.GetTempPath(),
                    "storage must be usable (tests, CLI tools) without app-level wiring"
                );
        }
        finally
        {
            StoragePaths.TempRoot = original;
        }
    }

    [Fact]
    public void TranscodeRoot_defaults_to_os_temp_path_when_not_overridden()
    {
        string original = StoragePaths.TranscodeRoot;
        try
        {
            StoragePaths.TranscodeRoot = Path.GetTempPath();

            StoragePaths.TranscodeRoot.Should().Be(Path.GetTempPath());
        }
        finally
        {
            StoragePaths.TranscodeRoot = original;
        }
    }

    [Fact]
    public void TempRoot_setter_overrides_the_value_hosts_read()
    {
        string original = StoragePaths.TempRoot;
        string overridden = Path.Combine(Path.GetTempPath(), $"nm-temproot-{Guid.NewGuid():N}");
        try
        {
            StoragePaths.TempRoot = overridden;

            StoragePaths
                .TempRoot.Should()
                .Be(
                    overridden,
                    "the host must be able to redirect staged files into its own data directory"
                );
        }
        finally
        {
            StoragePaths.TempRoot = original;
        }
    }

    [Fact]
    public void TranscodeRoot_setter_overrides_the_value_hosts_read()
    {
        string original = StoragePaths.TranscodeRoot;
        string overridden = Path.Combine(
            Path.GetTempPath(),
            $"nm-transcoderoot-{Guid.NewGuid():N}"
        );
        try
        {
            StoragePaths.TranscodeRoot = overridden;

            StoragePaths.TranscodeRoot.Should().Be(overridden);
        }
        finally
        {
            StoragePaths.TranscodeRoot = original;
        }
    }

    [Fact]
    public void TempRoot_and_TranscodeRoot_are_independent_seams()
    {
        // Regression guard: these are two DISTINCT static properties, not
        // aliases of the same backing field. Setting one must not affect the
        // other — a shared backing field would silently break either the
        // encoder's output path or remote-driver staging.
        string originalTemp = StoragePaths.TempRoot;
        string originalTranscode = StoragePaths.TranscodeRoot;
        try
        {
            string tempOverride = Path.Combine(Path.GetTempPath(), $"nm-temp-{Guid.NewGuid():N}");
            string transcodeOverride = Path.Combine(
                Path.GetTempPath(),
                $"nm-transcode-{Guid.NewGuid():N}"
            );

            StoragePaths.TempRoot = tempOverride;
            StoragePaths.TranscodeRoot = transcodeOverride;

            StoragePaths.TempRoot.Should().Be(tempOverride);
            StoragePaths.TranscodeRoot.Should().Be(transcodeOverride);
            StoragePaths.TempRoot.Should().NotBe(StoragePaths.TranscodeRoot);
        }
        finally
        {
            StoragePaths.TempRoot = originalTemp;
            StoragePaths.TranscodeRoot = originalTranscode;
        }
    }
}
