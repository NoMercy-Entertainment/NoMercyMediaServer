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

using NoMercy.Design;
using Xunit;

// Not `.Nm`: that namespace is visible as `Nm` from its neighbours and shadows
// the builder class of the same name, so every `Nm.Card(...)` beside it stops
// compiling.
namespace NoMercy.Tests.Api.NmComponents;

/// <summary>
/// The enumeration a kitchen sink draws from.
///
/// <para>
/// Generated from the manifest, so what is asserted here is that the generation
/// is sound rather than that someone remembered to list a component. A drawing
/// with a duplicate id silently overwrites another's screenshot, and a component
/// with only its default case is a component whose variants nothing proves.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class NmKitchenSinkTests
{
    [Fact]
    public void EveryComponentTheDesignSystemPublishesIsDrawn()
    {
        NmKitchenSink.Components.Should().HaveCount(56);
    }

    [Fact]
    public void EveryCaseNamesTheComponentItsPropsBelongTo()
    {
        foreach (NmKitchenSinkComponent component in NmKitchenSink.Components)
        {
            foreach (NmKitchenSinkCase drawing in component.Cases)
            {
                drawing.Props.Component.Should().Be(component.Component);
            }
        }
    }

    // A repeated id is two drawings sharing one screenshot, so the second is
    // never looked at and its variant is unproven while the run stays green.
    [Fact]
    public void EveryDrawingHasAnIdOfItsOwn()
    {
        NmKitchenSink
            .Components.SelectMany(component => component.Cases)
            .Select(drawing => drawing.Id)
            .Should()
            .OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryComponentIsDrawnAtLeastOnce()
    {
        foreach (NmKitchenSinkComponent component in NmKitchenSink.Components)
        {
            component.Cases.Should().NotBeEmpty(component.Component);
        }
    }

    // The point of the enumeration: a component that enumerates its options gets
    // one drawing per value, not one drawing and a claim of coverage.
    [Fact]
    public void AComponentWithChoicesIsDrawnOncePerChoice()
    {
        NmKitchenSinkComponent button = NmKitchenSink.Components.Single(component =>
            component.Component == "NMButton"
        );

        button.Cases.Should().HaveCountGreaterThan(1);
        button
            .Cases.Select(drawing => drawing.Label)
            .Should()
            .Contain(label => label.StartsWith("variant = ", StringComparison.Ordinal));
    }
}
