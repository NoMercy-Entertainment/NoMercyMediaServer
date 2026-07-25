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
using NoMercy.NmSystem.Configuration;

namespace NoMercy.Tests.NmSystem.Configuration;

[Trait("Category", "Unit")]
public class ContentPolicyTests
{
    [Fact]
    public void ShowAdultContent_FiresOn_ExplicitlyEnabled()
    {
        ContentPolicy policy = new() { AllowAdultContent = true };

        policy.ShowAdultContent.Should().BeTrue();
    }

    [Fact]
    public void ShowAdultContent_Silent_WhenExplicitlyDisabled()
    {
        ContentPolicy policy = new() { AllowAdultContent = false };

        policy.ShowAdultContent.Should().BeFalse();
    }

    [Fact]
    public void ShowAdultContent_Silent_WhenNeverConfigured()
    {
        ContentPolicy policy = new() { AllowAdultContent = null };

        policy.ShowAdultContent.Should().BeFalse();
    }

    [Fact]
    public void ShowAdultContent_DefaultsToHidden_OnFreshInstance()
    {
        ContentPolicy policy = new();

        policy.AllowAdultContent.Should().BeNull();
        policy.ShowAdultContent.Should().BeFalse();
    }

    [Theory]
    [InlineData([null, false])]
    [InlineData([false, false])]
    [InlineData([true, true])]
    public void ShowAdultContent_MatchesAllThreeInputStates(bool? configured, bool expected)
    {
        ContentPolicy policy = new() { AllowAdultContent = configured };

        policy.ShowAdultContent.Should().Be(expected);
    }

    [Fact]
    public void ShowAdultContent_AllValidNeighbors_AreDistinctFromFiresState()
    {
        ContentPolicy allowed = new() { AllowAdultContent = true };
        ContentPolicy nulled = new() { AllowAdultContent = null };
        ContentPolicy denied = new() { AllowAdultContent = false };

        bool fires = allowed.ShowAdultContent;
        bool silentNull = nulled.ShowAdultContent;
        bool silentFalse = denied.ShowAdultContent;

        fires.Should().BeTrue();
        silentNull.Should().BeFalse();
        silentFalse.Should().BeFalse();
    }

    [Fact]
    public void ShowAdultContent_IsComputed_NotMutable()
    {
        PropertyInfo property = typeof(ContentPolicy).GetProperty(
            nameof(ContentPolicy.ShowAdultContent)
        )!;

        property.Should().NotBeNull();
        property
            .CanWrite.Should()
            .BeFalse("ShowAdultContent must be a read-only computed guard, not a settable field");
    }

    [Fact]
    public void Meta_AllDecisionProperties_AreCoveredByThisSuite()
    {
        HashSet<string> knownDecisionProperties = new(StringComparer.Ordinal)
        {
            nameof(ContentPolicy.ShowAdultContent),
        };

        IEnumerable<PropertyInfo> computedBoolProperties = typeof(ContentPolicy)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool) && !p.CanWrite);

        foreach (PropertyInfo prop in computedBoolProperties)
        {
            knownDecisionProperties
                .Should()
                .Contain(
                    prop.Name,
                    $"computed bool '{prop.Name}' was added to ContentPolicy without a guard test — add it to knownDecisionProperties and write FIRES/SILENT tests"
                );
        }
    }
}
