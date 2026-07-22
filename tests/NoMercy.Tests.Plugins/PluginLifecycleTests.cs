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

using FluentAssertions;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginLifecycleTests
{
    private static PluginInfo CreatePluginInfo(PluginStatus status)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = "TestPlugin",
            Description = "Test",
            Version = new(major: 1, minor: 0, build: 0),
            Status = status,
        };
    }

    [Theory]
    [InlineData(data: [PluginStatus.Active, PluginStatus.Disabled])]
    [InlineData(data: [PluginStatus.Active, PluginStatus.Malfunctioned])]
    [InlineData(data: [PluginStatus.Active, PluginStatus.Deleted])]
    [InlineData(data: [PluginStatus.Disabled, PluginStatus.Active])]
    [InlineData(data: [PluginStatus.Disabled, PluginStatus.Deleted])]
    [InlineData(data: [PluginStatus.Malfunctioned, PluginStatus.Active])]
    [InlineData(data: [PluginStatus.Malfunctioned, PluginStatus.Disabled])]
    [InlineData(data: [PluginStatus.Malfunctioned, PluginStatus.Deleted])]
    public void CanTransition_AllowedTransitions_ReturnsTrue(PluginStatus from, PluginStatus to)
    {
        bool result = PluginLifecycle.CanTransition(from: from, to: to);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(data: [PluginStatus.Deleted, PluginStatus.Active])]
    [InlineData(data: [PluginStatus.Deleted, PluginStatus.Disabled])]
    [InlineData(data: [PluginStatus.Deleted, PluginStatus.Malfunctioned])]
    [InlineData(data: [PluginStatus.Disabled, PluginStatus.Malfunctioned])]
    [InlineData(data: [PluginStatus.Active, PluginStatus.Active])]
    [InlineData(data: [PluginStatus.Disabled, PluginStatus.Disabled])]
    public void CanTransition_ForbiddenTransitions_ReturnsFalse(PluginStatus from, PluginStatus to)
    {
        bool result = PluginLifecycle.CanTransition(from: from, to: to);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanTransition_FromStatusAbsentFromTheAllowedTransitionMap_ReturnsFalse()
    {
        // Every real PluginStatus value is a key in AllowedTransitions, so the
        // TryGetValue-miss branch is otherwise unreachable through the public
        // enum. An out-of-range cast is the only way to exercise it — proving
        // CanTransition fails closed (denies) for a status the map has no entry
        // for, rather than throwing or defaulting to permissive.
        PluginStatus unknownStatus = (PluginStatus)999;

        bool result = PluginLifecycle.CanTransition(from: unknownStatus, to: PluginStatus.Active);

        result.Should().BeFalse();
    }

    [Fact]
    public void Transition_ValidTransition_UpdatesStatus()
    {
        PluginInfo info = CreatePluginInfo(status: PluginStatus.Active);

        PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Disabled);

        info.Status.Should().Be(expected: PluginStatus.Disabled);
    }

    [Fact]
    public void Transition_InvalidTransition_ThrowsInvalidOperation()
    {
        PluginInfo info = CreatePluginInfo(status: PluginStatus.Deleted);

        Action act = () => PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Active);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*Deleted*Active*");
    }

    [Fact]
    public void Transition_NullInfo_ThrowsArgumentNullException()
    {
        Action act = () => PluginLifecycle.Transition(info: null!, newStatus: PluginStatus.Active);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Transition_ActiveToMalfunctioned_Succeeds()
    {
        PluginInfo info = CreatePluginInfo(status: PluginStatus.Active);

        PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Malfunctioned);

        info.Status.Should().Be(expected: PluginStatus.Malfunctioned);
    }

    [Fact]
    public void Transition_MalfunctionedToActive_Succeeds()
    {
        PluginInfo info = CreatePluginInfo(status: PluginStatus.Malfunctioned);

        PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Active);

        info.Status.Should().Be(expected: PluginStatus.Active);
    }

    [Fact]
    public void Transition_MalfunctionedToDisabled_Succeeds()
    {
        PluginInfo info = CreatePluginInfo(status: PluginStatus.Malfunctioned);

        PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Disabled);

        info.Status.Should().Be(expected: PluginStatus.Disabled);
    }

    [Fact]
    public void Transition_DisabledToMalfunctioned_Fails()
    {
        PluginInfo info = CreatePluginInfo(status: PluginStatus.Disabled);

        Action act = () => PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Malfunctioned);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Transition_FullLifecycle_ActiveToDisabledToActiveToDeleted()
    {
        PluginInfo info = CreatePluginInfo(status: PluginStatus.Active);

        PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Disabled);
        info.Status.Should().Be(expected: PluginStatus.Disabled);

        PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Active);
        info.Status.Should().Be(expected: PluginStatus.Active);

        PluginLifecycle.Transition(info: info, newStatus: PluginStatus.Deleted);
        info.Status.Should().Be(expected: PluginStatus.Deleted);
    }

    [Fact]
    public void Transition_DeletedIsTerminal_CannotTransitionToAnything()
    {
        PluginInfo info = CreatePluginInfo(status: PluginStatus.Deleted);

        foreach (PluginStatus status in Enum.GetValues<PluginStatus>())
        {
            PluginLifecycle
                .CanTransition(from: PluginStatus.Deleted, to: status)
                .Should()
                .BeFalse(because: $"Deleted should not transition to {status}");
        }
    }
}
