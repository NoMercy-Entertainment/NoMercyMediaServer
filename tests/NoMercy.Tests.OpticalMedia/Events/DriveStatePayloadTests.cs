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
            Method: method,
            Drive: "/dev/sr0",
            VolumeLabel: "MY_DISC",
            HasDisc: true,
            DiscType: "bluray",
            Timestamp: DateTime.UtcNow
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
        string jobId = Guid.NewGuid().ToString("N");

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

        payload.JobId.Should().Be(jobId);
        payload.Message.Should().Contain("42%");
        payload.DiscType.Should().Be("dvd");
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

        evt.DriveStateData.Should().BeSameAs(payload);
        evt.DriveStateData.Method.Should().Be("disc_inserted");
        evt.Source.Should().Be("DriveMonitor");
    }
}
