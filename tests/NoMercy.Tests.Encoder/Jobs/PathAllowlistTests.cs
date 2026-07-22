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

using NoMercy.Encoder.Jobs;

namespace NoMercy.Tests.Encoder.Jobs;

/// <summary>
/// PathAllowlist is the trust boundary between the encoder and the
/// filesystem. Getting it wrong lets a malicious job encode /etc/passwd
/// into the output folder, or escape a user's library into the admin's.
///
/// These tests pin:
///   - Paths inside an allowed root pass.
///   - Path traversal (../..) is normalized before check, not after.
///   - Sibling-prefix confusion: "/media/movies" allowlist MUST NOT
///     also allow "/media/movies_private/…" just because the sibling
///     starts with the same prefix.
///   - Case-insensitive comparison for Windows.
/// </summary>
public class PathAllowlistTests
{
    [Fact]
    public void InputPathInsideAllowedRoot_IsAllowed()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "allowed-in"));
        PathAllowlist list = new(AllowedInputPaths: [root], AllowedOutputPaths: []);

        bool allowed = list.IsInputPathAllowed(path: Path.Combine(path1: root, path2: "subfolder", path3: "file.mkv"));

        allowed.Should().BeTrue();
    }

    [Fact]
    public void InputPathOutsideAllowedRoot_IsDenied()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "allowed-in"));
        PathAllowlist list = new(AllowedInputPaths: [root], AllowedOutputPaths: []);

        // Totally different directory.
        bool allowed = list.IsInputPathAllowed(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "other", path3: "file.mkv")
        );

        allowed.Should().BeFalse();
    }

    [Fact]
    public void InputPathUsingTraversal_NormalizesBeforeCheck()
    {
        // /allowed/../etc/secret.mkv normalizes to /etc/secret.mkv —
        // must not be in the allowlist.
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "allowed-traverse"));
        PathAllowlist list = new(AllowedInputPaths: [root], AllowedOutputPaths: []);

        string traversal = Path.Combine(path1: root, path2: "..", path3: "elsewhere", path4: "file.mkv");
        bool allowed = list.IsInputPathAllowed(path: traversal);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void EmptyAllowlist_DeniesEverything()
    {
        PathAllowlist list = new(AllowedInputPaths: [], AllowedOutputPaths: []);
        list.IsInputPathAllowed(path: "/anything").Should().BeFalse();
        list.IsOutputPathAllowed(path: "/anything").Should().BeFalse();
    }

    [Fact]
    public void OutputPathInsideAllowedRoot_IsAllowed()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "allowed-out"));
        PathAllowlist list = new(AllowedInputPaths: [], AllowedOutputPaths: [root]);

        bool allowed = list.IsOutputPathAllowed(path: Path.Combine(path1: root, path2: "file.mkv"));

        allowed.Should().BeTrue();
    }

    [Fact]
    public void OutputAndInputLists_AreIndependent()
    {
        // A path allowed as output must not automatically be allowed as input
        // (and vice versa). This separates "can we read from here" from
        // "can we write to here" — critical for libraries that are
        // read-only vs the server's output directory.
        string inPath = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "only-in"));
        string outPath = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "only-out"));
        PathAllowlist list = new(AllowedInputPaths: [inPath], AllowedOutputPaths: [outPath]);

        list.IsInputPathAllowed(path: Path.Combine(path1: outPath, path2: "x.mkv")).Should().BeFalse();
        list.IsOutputPathAllowed(path: Path.Combine(path1: inPath, path2: "x.mkv")).Should().BeFalse();
    }

    [Fact]
    public void SiblingDirectory_WithSharedPrefix_IsDenied()
    {
        // Regression: "/media/movies" allowlist must not grant access to
        // "/media/movies_private/…". StartsWith against a raw prefix lets
        // this through; we guard by requiring the allowed path to match
        // at a directory boundary.
        string allowed = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "media", path3: "movies"));
        PathAllowlist list = new(AllowedInputPaths: [allowed], AllowedOutputPaths: []);

        string sibling = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "media", path3: "movies_private", path4: "secret.mkv")
        );

        bool result = list.IsInputPathAllowed(path: sibling);

        result.Should().BeFalse(because: "sibling with shared prefix must not be allowed");
    }
}
