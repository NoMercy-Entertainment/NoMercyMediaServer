// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
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
