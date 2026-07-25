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

using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Two <see cref="IStorage"/> default-interface-member branches
/// <see cref="IStorageFacadeTests"/> doesn't reach: <c>GetName("")</c> and
/// <c>GetParent</c> on a path with a leading separator and nothing before
/// it (e.g. <c>"/file.txt"</c>), where the "parent" segment computes to an
/// empty string rather than genuinely having no separator at all.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IStorageDefaultMembersAdditionalTests
{
    private static IStorage NewStorage()
    {
        string root = Path.Combine(Path.GetTempPath(), $"nm-istorage-defaults-{Guid.NewGuid():N}");
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new([root], driver);
        return new LocalStorage(driver, guard);
    }

    [Fact]
    public void GetName_of_empty_path_returns_empty_string()
    {
        IStorage storage = NewStorage();

        storage.GetName(string.Empty).Should().Be(string.Empty);
    }

    [Fact]
    public void GetParent_of_a_leading_slash_path_returns_null_not_empty_string()
    {
        // "/file.txt" has a separator (idx=0) but nothing before it — the
        // computed parent is "", which must normalize to null (no parent),
        // not be returned as a literal empty-string parent.
        IStorage storage = NewStorage();

        storage.GetParent("/file.txt").Should().BeNull();
    }
}
