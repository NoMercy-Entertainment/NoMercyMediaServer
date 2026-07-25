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

using NoMercy.Cli.Commands;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>BuildQuery</c> always includes <c>tail</c>, and adds
/// <c>levels</c>/<c>types</c> only when the caller actually supplied a
/// non-blank filter — an all-whitespace filter must be treated the same as
/// "not provided", and any filter value must be percent-encoded so a value
/// containing a comma or space cannot corrupt the query string.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LogsCommandBuildQueryTests
{
    [Fact]
    public void BuildQuery_NoFilters_ReturnsTailOnly()
    {
        LogsCommand.BuildQuery(100, null, null).Should().Be("?tail=100");
    }

    [Fact]
    public void BuildQuery_WhitespaceOnlyFilters_TreatedAsAbsent()
    {
        LogsCommand.BuildQuery(50, "   ", "\t").Should().Be("?tail=50");
    }

    [Fact]
    public void BuildQuery_LevelOnly_AppendsLevelsParameter()
    {
        LogsCommand.BuildQuery(10, "Error", null).Should().Be("?tail=10&levels=Error");
    }

    [Fact]
    public void BuildQuery_TypeOnly_AppendsTypesParameter()
    {
        LogsCommand.BuildQuery(10, null, "App").Should().Be("?tail=10&types=App");
    }

    [Fact]
    public void BuildQuery_LevelAndType_AppendsBothInOrder()
    {
        LogsCommand.BuildQuery(25, "Error", "App").Should().Be("?tail=25&levels=Error&types=App");
    }

    [Fact]
    public void BuildQuery_CommaSeparatedLevels_PercentEncodesTheComma()
    {
        LogsCommand
            .BuildQuery(10, "Error,Warning", null)
            .Should()
            .Be("?tail=10&levels=Error%2CWarning");
    }
}
