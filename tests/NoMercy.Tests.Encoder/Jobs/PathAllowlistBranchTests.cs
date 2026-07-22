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
/// Branch coverage for PathAllowlist edges the happy-path tests miss:
/// the path can BE the allowed root (no descendant), multiple roots
/// resolve independently, case-insensitive comparison on Windows, and
/// trailing separators on either side of the comparison are tolerated.
/// Each one is a real cliff in a security-critical class.
/// </summary>
public class PathAllowlistBranchTests
{
    [Fact]
    public void PathIsExactRoot_IsAllowed()
    {
        // Allowlist root "/media/movies"; query "/media/movies" itself.
        // Without the exact-match branch, only descendants pass — an
        // encode request that targets the root dir would be denied.
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "exact-root"));
        PathAllowlist list = new(AllowedInputPaths: [root], AllowedOutputPaths: []);

        list.IsInputPathAllowed(path: root)
            .Should()
            .BeTrue(because: "the root itself must be inside the allowlist");
    }

    [Fact]
    public void RootWithTrailingSeparator_NormalizedBeforeCompare()
    {
        // Allowlist entry includes the trailing separator. Internal code
        // strips it before comparison; verify a descendant query still
        // matches.
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "trailing-root"));
        string rootWithSlash = root + Path.DirectorySeparatorChar;
        PathAllowlist list = new(AllowedInputPaths: [rootWithSlash], AllowedOutputPaths: []);

        list.IsInputPathAllowed(path: Path.Combine(path1: root, path2: "child.mkv")).Should().BeTrue();
        list.IsInputPathAllowed(path: root).Should().BeTrue();
    }

    [Fact]
    public void MultipleRoots_EachIsCheckedIndependently()
    {
        // Two unrelated allowed roots — a query inside the second one
        // must pass even though the first one doesn't match.
        string first = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "first-root"));
        string second = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "second-root"));
        PathAllowlist list = new(AllowedInputPaths: [first, second], AllowedOutputPaths: []);

        list.IsInputPathAllowed(path: Path.Combine(path1: first, path2: "a.mkv")).Should().BeTrue();
        list.IsInputPathAllowed(path: Path.Combine(path1: second, path2: "b.mkv")).Should().BeTrue();
        list.IsInputPathAllowed(path: Path.Combine(path1: Path.GetTempPath(), path2: "third-root", path3: "c.mkv"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void PathCaseDiffersFromRoot_IsAllowed_OnCaseInsensitiveCompare()
    {
        // Windows paths are case-insensitive. Linux is technically
        // case-sensitive, but the encoder uses OrdinalIgnoreCase for
        // allowlist matching so a config typed "/Media/Movies" matches
        // an actual "/media/movies" entry — this is the safer default
        // for a user-facing config.
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "case-root"));
        PathAllowlist list = new(AllowedInputPaths: [root.ToLowerInvariant()], AllowedOutputPaths: []);

        string upperPath = Path.Combine(path1: root.ToUpperInvariant(), path2: "FILE.MKV");
        list.IsInputPathAllowed(path: upperPath).Should().BeTrue();
    }

    [Fact]
    public void PathThatIsRootPlusExtension_IsDenied()
    {
        // "/media/movies" allowed; query "/media/movies.bak" must NOT match.
        // Without the directory-boundary check this would slip through
        // because the comparison would see ".bak" as a continuation.
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "ext-root"));
        PathAllowlist list = new(AllowedInputPaths: [root], AllowedOutputPaths: []);

        string sibling = root + ".bak";
        list.IsInputPathAllowed(path: sibling)
            .Should()
            .BeFalse(because: "a path that extends the root is not under it");
    }

    [Fact]
    public void OutputAllowlist_SameSemanticsAsInput()
    {
        // The two lists run through the same IsUnderAny helper. Verify
        // that the descendant + exact-match + boundary checks all apply
        // to the output list too.
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "out-root"));
        PathAllowlist list = new(AllowedInputPaths: [], AllowedOutputPaths: [root]);

        list.IsOutputPathAllowed(path: root).Should().BeTrue(because: "exact root");
        list.IsOutputPathAllowed(path: Path.Combine(path1: root, path2: "x.mkv")).Should().BeTrue(because: "descendant");
        list.IsOutputPathAllowed(path: root + ".other").Should().BeFalse(because: "prefix-extended sibling");
    }
}
