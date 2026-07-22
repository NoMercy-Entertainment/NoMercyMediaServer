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

using Moq;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait(name: "Category", value: "Unit")]
public class DiscSourceFactoryTests
{
    [Fact]
    public void CreateFor_MatchesRegisteredType_ReturnsSource()
    {
        Mock<IDiscSource> bluraySource = new();
        bluraySource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.BluRay);

        Mock<IDiscSource> dvdSource = new();
        dvdSource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.Dvd);

        DiscSourceFactory sut = new(sources: [bluraySource.Object, dvdSource.Object]);

        IDiscSource? result = sut.CreateFor(type: OpticalDiscType.Dvd);

        result.Should().Be(expected: dvdSource.Object);
    }

    [Fact]
    public void CreateFor_UnregisteredType_ReturnsNull()
    {
        Mock<IDiscSource> bluraySource = new();
        bluraySource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.BluRay);

        DiscSourceFactory sut = new(sources: [bluraySource.Object]);

        IDiscSource? result = sut.CreateFor(type: OpticalDiscType.Dvd);

        result.Should().BeNull();
    }

    [Fact]
    public void CreateFor_NoneType_ReturnsNull()
    {
        Mock<IDiscSource> bluraySource = new();
        bluraySource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.BluRay);

        DiscSourceFactory sut = new(sources: [bluraySource.Object]);

        IDiscSource? result = sut.CreateFor(type: OpticalDiscType.None);

        result.Should().BeNull();
    }

    [Fact]
    public void CreateFor_MultipleSourcesRegistered_ReturnsCorrectOne()
    {
        Mock<IDiscSource> cdSource = new();
        cdSource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.Cd);

        Mock<IDiscSource> dvdSource = new();
        dvdSource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.Dvd);

        Mock<IDiscSource> bluraySource = new();
        bluraySource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.BluRay);

        DiscSourceFactory sut = new(sources: [cdSource.Object, dvdSource.Object, bluraySource.Object]);

        IDiscSource? result = sut.CreateFor(type: OpticalDiscType.BluRay);

        result.Should().Be(expected: bluraySource.Object);
    }

    [Fact]
    public void CreateFor_CdType_ReturnsCorrectSource()
    {
        Mock<IDiscSource> cdSource = new();
        cdSource.Setup(expression: s => s.Type).Returns(value: OpticalDiscType.Cd);

        DiscSourceFactory sut = new(sources: [cdSource.Object]);

        IDiscSource? result = sut.CreateFor(type: OpticalDiscType.Cd);

        result.Should().Be(expected: cdSource.Object);
    }

    [Fact]
    public void CreateFor_NoSourcesRegistered_ReturnsNull()
    {
        DiscSourceFactory sut = new(sources: []);

        IDiscSource? result = sut.CreateFor(type: OpticalDiscType.BluRay);

        result.Should().BeNull();
    }
}
