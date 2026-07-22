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

[Trait(name: "Category", value: "Unit")]
public class DriveStatePayloadTests
{
    [Theory]
    [InlineData(data: "drive_added")]
    [InlineData(data: "drive_removed")]
    [InlineData(data: "disc_inserted")]
    [InlineData(data: "disc_ejected")]
    [InlineData(data: "drive_changed")]
    [InlineData(data: "rip_started")]
    [InlineData(data: "rip_progress")]
    [InlineData(data: "rip_complete")]
    [InlineData(data: "rip_error")]
    [InlineData(data: "rip_pending")]
    public void KnownMethod_RoundTrips_WithRequiredFields(string method)
    {
        DriveStatePayload payload = new(
            Method: method,
            Drive: "/dev/sr0",
            VolumeLabel: "MY_DISC",
            HasDisc: true,
            DiscType: "bluray",
            Timestamp: DateTime.UtcNow
        );

        payload.Method.Should().Be(expected: method);
        payload.Drive.Should().Be(expected: "/dev/sr0");
        payload.HasDisc.Should().BeTrue();
        payload.DiscType.Should().Be(expected: "bluray");
    }

    [Fact]
    public void OptionalFields_DefaultToNull()
    {
        DriveStatePayload payload = new(
            Method: "drive_added",
            Drive: "D:\\",
            VolumeLabel: null,
            HasDisc: false,
            DiscType: "none",
            Timestamp: DateTime.UtcNow
        );

        payload.JobId.Should().BeNull();
        payload.Message.Should().BeNull();
        payload.VolumeLabel.Should().BeNull();
    }

    [Fact]
    public void RipProgress_Fields_PopulatedCorrectly()
    {
        string jobId = Guid.NewGuid().ToString(format: "N");

        DriveStatePayload payload = new(
            Method: "rip_progress",
            Drive: "D:\\",
            VolumeLabel: "MOVIE_2023",
            HasDisc: true,
            DiscType: "dvd",
            Timestamp: DateTime.UtcNow,
            JobId: jobId,
            Message: "Ripping title 01 — 42%"
        );

        payload.JobId.Should().Be(expected: jobId);
        payload.Message.Should().Contain(expected: "42%");
        payload.DiscType.Should().Be(expected: "dvd");
    }

    [Fact]
    public void DriveStateChangedEvent_CarriesTypedPayload()
    {
        DriveStatePayload payload = new(
            Method: "disc_inserted",
            Drive: "/dev/sr0",
            VolumeLabel: "LABEL",
            HasDisc: true,
            DiscType: "cd",
            Timestamp: DateTime.UtcNow
        );

        DriveStateChangedEvent evt = new() { DriveStateData = payload };

        evt.DriveStateData.Should().BeSameAs(expected: payload);
        evt.DriveStateData.Method.Should().Be(expected: "disc_inserted");
        evt.Source.Should().Be(expected: "DriveMonitor");
    }
}
