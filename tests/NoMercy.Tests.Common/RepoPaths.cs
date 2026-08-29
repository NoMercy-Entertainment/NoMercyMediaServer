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

using System.Runtime.CompilerServices;

namespace NoMercy.Tests.Common;

/// <summary>
/// Where the source is, for the tests that read it.
///
/// <para>
/// A source-reading test used to walk up from its own build output looking for
/// a folder called src. That works only while the output sits inside the
/// repository, and it does not when the build is redirected: the running server
/// holds its output open, so a build beside it has to write somewhere else, and
/// from there the walk never meets the repository at all. Twenty-six tests in
/// ten files failed that way, each with its own copy of the same walk and its
/// own hardcoded fallback path.
/// </para>
///
/// <para>
/// This file's own path is stamped in at compile time, so it points at the
/// checkout the assembly was built from wherever the output was written.
/// </para>
/// </summary>
public static class RepoPaths
{
    /// <summary>The repository root: the folder holding src and tests.</summary>
    public static string Root { get; } = ResolveRoot();

    /// <summary>The server's source tree.</summary>
    public static string Src { get; } = Path.Combine(Root, "src");

    /// <summary>A path under the repository root, by its parts.</summary>
    public static string File(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>The one source file with this name, anywhere under src.</summary>
    public static string SourceFile(string fileName)
    {
        string[] matches = Directory.GetFiles(Src, fileName, SearchOption.AllDirectories);

        if (matches.Length == 0)
            throw new FileNotFoundException($"No {fileName} under {Src}");

        return matches[0];
    }

    private static string ResolveRoot([CallerFilePath] string thisFile = "")
    {
        // Climbing rather than counting folders, so moving this file cannot
        // quietly point it at the wrong place. The climb starts inside the
        // checkout whatever the build wrote its output to.
        string? directory = Path.GetDirectoryName(thisFile);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory, "src")))
                return directory;

            directory = Path.GetDirectoryName(directory);
        }

        throw new DirectoryNotFoundException(
            $"No folder above {thisFile} holds a src directory, so this file is not inside "
                + "the checkout it was compiled from."
        );
    }
}
