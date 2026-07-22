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

using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="StoragePathGuardTests"/> exercises <c>StructuralValidate</c> and
/// <c>IsRootedAnyStyle</c> against empty strings but never a literal
/// <c>null</c> — a distinct branch for both methods (Rule 3 says empty means
/// "scope root", and a null caller-supplied path must hit the exact same
/// early-return, not a <see cref="NullReferenceException"/>).
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class StoragePathGuardNullInputTests
{
    [Fact]
    public void StructuralValidate_accepts_null_the_same_as_empty()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: null);

        act.Should()
            .NotThrow(because: "null must be treated as the scope root just like empty string (Rule 3)");
    }

    [Fact]
    public void IsRootedAnyStyle_returns_false_for_null()
    {
        StoragePathGuard.IsRootedAnyStyle(path: null).Should().BeFalse();
    }
}
