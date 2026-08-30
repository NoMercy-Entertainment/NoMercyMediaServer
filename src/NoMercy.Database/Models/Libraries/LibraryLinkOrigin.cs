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

namespace NoMercy.Database.Models.Libraries;

/// <summary>
/// Why a title is in a library.
/// <para>
/// A row someone asked for and a row a scan brought in are two different things,
/// and until this existed nothing in the data said which was which.
/// </para>
/// </summary>
public static class LibraryLinkOrigin
{
    /// <summary>Someone pressed add.</summary>
    public const string Manual = "manual";

    /// <summary>A file on disk brought it in - the watcher, the inbox, a scan.</summary>
    public const string File = "file";
}
