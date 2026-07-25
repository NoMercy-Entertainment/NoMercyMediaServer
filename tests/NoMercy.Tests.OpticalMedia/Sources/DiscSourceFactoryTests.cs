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

[Trait("Category", "Unit")]
public class DiscSourceFactoryTests
{
    [Fact]
    public void CreateFor_MatchesRegisteredType_ReturnsSource()
    {
        Mock<IDiscSource> bluraySource = new();
        bluraySource.Setup(s => s.Type).Returns(OpticalDiscType.BluRay);

        Mock<IDiscSource> dvdSource = new();
        dvdSource.Setup(s => s.Type).Returns(OpticalDiscType.Dvd);

        DiscSourceFactory sut = new([bluraySource.Object, dvdSource.Object]);

        IDiscSource? result = sut.CreateFor(OpticalDiscType.Dvd);

        result.Should().Be(dvdSource.Object);
    }

    [Fact]
    public void CreateFor_UnregisteredType_ReturnsNull()
    {
        Mock<IDiscSource> bluraySource = new();
        bluraySource.Setup(s => s.Type).Returns(OpticalDiscType.BluRay);

        DiscSourceFactory sut = new([bluraySource.Object]);

        IDiscSource? result = sut.CreateFor(OpticalDiscType.Dvd);

        result.Should().BeNull();
    }

    [Fact]
    public void CreateFor_NoneType_ReturnsNull()
    {
        Mock<IDiscSource> bluraySource = new();
        bluraySource.Setup(s => s.Type).Returns(OpticalDiscType.BluRay);

        DiscSourceFactory sut = new([bluraySource.Object]);

        IDiscSource? result = sut.CreateFor(OpticalDiscType.None);

        result.Should().BeNull();
    }

    [Fact]
    public void CreateFor_MultipleSourcesRegistered_ReturnsCorrectOne()
    {
        Mock<IDiscSource> cdSource = new();
        cdSource.Setup(s => s.Type).Returns(OpticalDiscType.Cd);

        Mock<IDiscSource> dvdSource = new();
        dvdSource.Setup(s => s.Type).Returns(OpticalDiscType.Dvd);

        Mock<IDiscSource> bluraySource = new();
        bluraySource.Setup(s => s.Type).Returns(OpticalDiscType.BluRay);

        DiscSourceFactory sut = new([cdSource.Object, dvdSource.Object, bluraySource.Object]);

        IDiscSource? result = sut.CreateFor(OpticalDiscType.BluRay);

        result.Should().Be(bluraySource.Object);
    }

    [Fact]
    public void CreateFor_CdType_ReturnsCorrectSource()
    {
        Mock<IDiscSource> cdSource = new();
        cdSource.Setup(s => s.Type).Returns(OpticalDiscType.Cd);

        DiscSourceFactory sut = new([cdSource.Object]);

        IDiscSource? result = sut.CreateFor(OpticalDiscType.Cd);

        result.Should().Be(cdSource.Object);
    }

    [Fact]
    public void CreateFor_NoSourcesRegistered_ReturnsNull()
    {
        DiscSourceFactory sut = new([]);

        IDiscSource? result = sut.CreateFor(OpticalDiscType.BluRay);

        result.Should().BeNull();
    }
}
