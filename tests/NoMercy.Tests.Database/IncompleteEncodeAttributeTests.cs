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

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database.Models.Encoder;

namespace NoMercy.Tests.Database;

public class IncompleteEncodeAttributeTests
{
    [Fact]
    public void IncompleteEncode_HasPrimaryKey_OnId()
    {
        IEnumerable<PrimaryKeyAttribute> attrs =
            typeof(IncompleteEncode).GetCustomAttributes<PrimaryKeyAttribute>();
        bool found = attrs.Any(pk => pk.PropertyNames.Contains(nameof(IncompleteEncode.Id)));
        Assert.True(found, "IncompleteEncode must have [PrimaryKey(nameof(Id))]");
    }

    [Fact]
    public void IncompleteEncode_HasUniqueCompositeIndex_OnMediaIdAndFolderId()
    {
        IEnumerable<IndexAttribute> attrs =
            typeof(IncompleteEncode).GetCustomAttributes<IndexAttribute>();
        bool found = attrs.Any(ix =>
            ix.IsUnique
            && ix.PropertyNames.Contains(nameof(IncompleteEncode.MediaId))
            && ix.PropertyNames.Contains(nameof(IncompleteEncode.FolderId))
        );
        Assert.True(
            found,
            "IncompleteEncode must have a unique composite [Index] on MediaId + FolderId"
        );
    }
}
