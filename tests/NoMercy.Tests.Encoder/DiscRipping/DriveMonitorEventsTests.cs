using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Drives.Backends;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// Verifies the event payload shape produced by <see cref="DriveMonitor"/> and the
/// <see cref="DriveEvent"/> / <see cref="DriveEventType"/> records used to carry
/// drive state to the SignalR hub.
/// </summary>
public class DriveMonitorEventsTests
{
    // ── DriveEvent record shape ──────────────────────────────────────────────

    [Fact]
    public void DriveEvent_DiscInserted_HasExpectedShape()
    {
        DiscDrive drive = new("D:\\", "MY_DISC", true, OpticalDiscType.BluRay);
        DriveEvent evt = new(DriveEventType.DiscInserted, drive);

        evt.Type.Should().Be(DriveEventType.DiscInserted);
        evt.Drive.Path.Should().Be("D:\\");
        evt.Drive.Label.Should().Be("MY_DISC");
        evt.Drive.HasDisc.Should().BeTrue();
        evt.Drive.DiscType.Should().Be(OpticalDiscType.BluRay);
    }

    [Fact]
    public void DriveEvent_DiscEjected_HasExpectedShape()
    {
        DiscDrive drive = new("E:\\", string.Empty, false, OpticalDiscType.None);
        DriveEvent evt = new(DriveEventType.DiscEjected, drive);

        evt.Type.Should().Be(DriveEventType.DiscEjected);
        evt.Drive.HasDisc.Should().BeFalse();
        evt.Drive.Label.Should().BeEmpty();
    }

    [Fact]
    public void DriveEvent_DriveAdded_HasExpectedShape()
    {
        DiscDrive drive = new("F:\\", string.Empty, false, OpticalDiscType.None);
        DriveEvent evt = new(DriveEventType.DriveAdded, drive);

        evt.Type.Should().Be(DriveEventType.DriveAdded);
        evt.Drive.Path.Should().Be("F:\\");
    }

    [Fact]
    public void DriveEvent_DriveRemoved_HasExpectedShape()
    {
        DiscDrive drive = new("G:\\", string.Empty, false, OpticalDiscType.None);
        DriveEvent evt = new(DriveEventType.DriveRemoved, drive);

        evt.Type.Should().Be(DriveEventType.DriveRemoved);
    }

    // ── DriveEventType covers all four cases ────────────────────────────────

    [Theory]
    [InlineData(DriveEventType.DriveAdded)]
    [InlineData(DriveEventType.DriveRemoved)]
    [InlineData(DriveEventType.DiscInserted)]
    [InlineData(DriveEventType.DiscEjected)]
    public void DriveEventType_AllVariantsRoundTripThroughRecord(DriveEventType kind)
    {
        DiscDrive drive = new("H:\\", string.Empty, false, OpticalDiscType.None);
        DriveEvent evt = new(kind, drive);

        evt.Type.Should().Be(kind);
    }

    // ── DiscDrive record equality (value semantics) ──────────────────────────

    [Fact]
    public void DiscDrive_SameValues_AreEqual()
    {
        DiscDrive a = new("D:\\", "LABEL", true, OpticalDiscType.Dvd);
        DiscDrive b = new("D:\\", "LABEL", true, OpticalDiscType.Dvd);

        a.Should().Be(b);
    }

    [Fact]
    public void DiscDrive_DifferentPath_AreNotEqual()
    {
        DiscDrive a = new("D:\\", "LABEL", true, OpticalDiscType.Dvd);
        DiscDrive b = new("E:\\", "LABEL", true, OpticalDiscType.Dvd);

        a.Should().NotBe(b);
    }

    // ── GetDrives returns consistent shape ───────────────────────────────────

    [Fact]
    public void GetDrives_AllReturnedDrivesHaveNonEmptyPath()
    {
        DriveMonitor monitor = new(new PollingDriveBackend());

        IReadOnlyList<DiscDrive> drives = monitor.GetDrives();

        foreach (DiscDrive drive in drives)
        {
            drive.Path.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetDrives_DiscTypeIsNoneWhenNoDisc()
    {
        DriveMonitor monitor = new(new PollingDriveBackend());

        IReadOnlyList<DiscDrive> drives = monitor.GetDrives();

        foreach (DiscDrive drive in drives.Where(d => !d.HasDisc))
        {
            drive.DiscType.Should().Be(OpticalDiscType.None);
        }
    }
}
