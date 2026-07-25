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

using NoMercy.Events.DriveMonitor;

namespace NoMercy.Tests.OpticalMedia.Events;

[Trait("Category", "Unit")]
public class DriveStatePayloadTests
{
    [Theory]
    [InlineData("drive_added")]
    [InlineData("drive_removed")]
    [InlineData("disc_inserted")]
    [InlineData("disc_ejected")]
    [InlineData("drive_changed")]
    [InlineData("rip_started")]
    [InlineData("rip_progress")]
    [InlineData("rip_complete")]
    [InlineData("rip_error")]
    [InlineData("rip_pending")]
    public void KnownMethod_RoundTrips_WithRequiredFields(string method)
    {
        DriveStatePayload payload = new(
            method,
            "/dev/sr0",
            "MY_DISC",
            true,
            "bluray",
            DateTime.UtcNow
        );

        payload.Method.Should().Be(method);
        payload.Drive.Should().Be("/dev/sr0");
        payload.HasDisc.Should().BeTrue();
        payload.DiscType.Should().Be("bluray");
    }

    [Fact]
    public void OptionalFields_DefaultToNull()
    {
        DriveStatePayload payload = new(
            "drive_added",
            "D:\\",
            null,
            false,
            "none",
            DateTime.UtcNow
        );

        payload.JobId.Should().BeNull();
        payload.Message.Should().BeNull();
        payload.VolumeLabel.Should().BeNull();
    }

    [Fact]
    public void RipProgress_Fields_PopulatedCorrectly()
    {
        string jobId = Guid.NewGuid().ToString("N");

        DriveStatePayload payload = new(
            "rip_progress",
            "D:\\",
            "MOVIE_2023",
            true,
            "dvd",
            DateTime.UtcNow,
            jobId,
            "Ripping title 01 — 42%"
        );

        payload.JobId.Should().Be(jobId);
        payload.Message.Should().Contain("42%");
        payload.DiscType.Should().Be("dvd");
    }

    [Fact]
    public void DriveStateChangedEvent_CarriesTypedPayload()
    {
        DriveStatePayload payload = new(
            "disc_inserted",
            "/dev/sr0",
            "LABEL",
            true,
            "cd",
            DateTime.UtcNow
        );

        DriveStateChangedEvent evt = new() { DriveStateData = payload };

        evt.DriveStateData.Should().BeSameAs(payload);
        evt.DriveStateData.Method.Should().Be("disc_inserted");
        evt.Source.Should().Be("DriveMonitor");
    }
}
