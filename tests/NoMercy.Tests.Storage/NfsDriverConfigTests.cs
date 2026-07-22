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

using NoMercy.Storage.Drivers.Nfs;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="NfsDriverConfig.Parse"/> validation not already covered by the
/// happy-path construction in <c>NfsStorageDriverTests</c> — specifically the
/// "JSON parsed to a null object" case, which is distinct from "JSON failed
/// to parse" (a syntactically valid <c>null</c> literal is valid JSON but an
/// invalid config).
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class NfsDriverConfigTests
{
    [Fact]
    public void Parse_json_null_literal_throws()
    {
        Action act = () => NfsDriverConfig.Parse(json: "null", folderId: Ulid.NewUlid());

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*null*");
    }
}
