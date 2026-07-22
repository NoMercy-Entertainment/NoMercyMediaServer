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

using NoMercy.OpticalMedia.Metadata;

namespace NoMercy.Tests.OpticalMedia.Metadata;

[Trait(name: "Category", value: "Unit")]
public class WindowsTocReaderTests
{
    // ── Canonical fixture helpers ─────────────────────────────────────────────

    // 6-track canonical disc matching the MusicBrainz Disc ID spec fixture:
    //   track absolute frames : [150, 15363, 32314, 46592, 63414, 80489]
    //   lead-out absolute     : 95462
    //   expected disc id      : 49HHV7Eb8UKF3aQiNmu1GR8vKTY-

    private static readonly int[] CanonicalTrackOffsets = [150, 15363, 32314, 46592, 63414, 80489];

    private const int CanonicalLeadOut = 95462;
    private const string CanonicalDiscId = "49HHV7Eb8UKF3aQiNmu1GR8vKTY-";

    /// <summary>
    /// Converts an absolute frame offset back to MSF bytes.
    /// M = f / (75*60), S = (f / 75) % 60, F = f % 75.
    /// </summary>
    private static (byte Minute, byte Second, byte Frame) AbsoluteToMsf(int absolute) =>
        (
            Minute: (byte)(absolute / (75 * 60)),
            Second: (byte)((absolute / 75) % 60),
            Frame: (byte)(absolute % 75)
        );

    /// <summary>
    /// Builds a well-formed 804-byte CDROM_TOC buffer for the given tracks
    /// and lead-out. Entries are placed in track-number order followed by
    /// the 0xAA lead-out entry. Remaining slots are left as zero bytes.
    /// </summary>
    private static byte[] BuildTocBuffer(
        byte firstTrack,
        byte lastTrack,
        int[] trackAbsolutes,
        int leadOutAbsolute
    )
    {
        byte[] buffer = new byte[804];

        // Header: Length (big-endian, value 802), FirstTrack, LastTrack.
        buffer[0] = 0x03;
        buffer[1] = 0x22;
        buffer[2] = firstTrack;
        buffer[3] = lastTrack;

        // Write track entries starting at byte 4, 8 bytes each.
        int entryIndex = 0;

        for (int trackIndex = 0; trackIndex < trackAbsolutes.Length; trackIndex++)
        {
            (byte minute, byte second, byte frame) = AbsoluteToMsf(absolute: trackAbsolutes[trackIndex]);
            int offset = 4 + entryIndex * 8;

            buffer[offset + 0] = 0x00; // Reserved
            buffer[offset + 1] = 0x14; // Adr=1, Control=4 (data track bits; audio=0x10)
            buffer[offset + 2] = (byte)(firstTrack + trackIndex); // TrackNumber
            buffer[offset + 3] = 0x00; // Reserved1
            buffer[offset + 4] = 0x00; // Addr0 (unused in MSF mode)
            buffer[offset + 5] = minute;
            buffer[offset + 6] = second;
            buffer[offset + 7] = frame;

            entryIndex++;
        }

        // Lead-out entry (TrackNumber = 0xAA).
        {
            (byte minute, byte second, byte frame) = AbsoluteToMsf(absolute: leadOutAbsolute);
            int offset = 4 + entryIndex * 8;

            buffer[offset + 0] = 0x00;
            buffer[offset + 1] = 0x14;
            buffer[offset + 2] = 0xAA;
            buffer[offset + 3] = 0x00;
            buffer[offset + 4] = 0x00;
            buffer[offset + 5] = minute;
            buffer[offset + 6] = second;
            buffer[offset + 7] = frame;
        }

        return buffer;
    }

    // ── ParseCdromToc — canonical fixture round-trip ──────────────────────────

    /// <summary>
    /// End-to-end proof without hardware: build a CDROM_TOC byte[] for the
    /// canonical 6-track disc, parse it, assert the DiscToc offsets are exact,
    /// then assert MusicBrainzDiscId.Compute produces the expected disc id.
    /// </summary>
    [Fact]
    public void ParseCdromToc_CanonicalFixture_RoundTripsToExpectedDiscId()
    {
        byte[] buffer = BuildTocBuffer(firstTrack: 1, lastTrack: 6, trackAbsolutes: CanonicalTrackOffsets, leadOutAbsolute: CanonicalLeadOut);

        DiscToc toc = WindowsTocReader.ParseCdromToc(tocBuffer: buffer);

        toc.FirstTrack.Should().Be(expected: 1);
        toc.LastTrack.Should().Be(expected: 6);
        toc.LeadOutOffsetSectors.Should().Be(expected: CanonicalLeadOut);
        toc.TrackOffsetsSectors.Should().Equal(elements: CanonicalTrackOffsets);

        string discId = MusicBrainzDiscId.Compute(toc: toc);
        discId.Should().Be(expected: CanonicalDiscId);
    }

    // ── ParseCdromToc — per-track offset assertions ───────────────────────────

    [Fact]
    public void ParseCdromToc_CanonicalFixture_TrackCount()
    {
        byte[] buffer = BuildTocBuffer(firstTrack: 1, lastTrack: 6, trackAbsolutes: CanonicalTrackOffsets, leadOutAbsolute: CanonicalLeadOut);
        DiscToc toc = WindowsTocReader.ParseCdromToc(tocBuffer: buffer);

        toc.TrackOffsetsSectors.Should().HaveCount(expected: 6);
    }

    [Theory]
    [InlineData(data: [0, 150])]
    [InlineData(data: [1, 15363])]
    [InlineData(data: [2, 32314])]
    [InlineData(data: [3, 46592])]
    [InlineData(data: [4, 63414])]
    [InlineData(data: [5, 80489])]
    public void ParseCdromToc_CanonicalFixture_IndividualTrackOffset(
        int trackIndex,
        int expectedOffset
    )
    {
        byte[] buffer = BuildTocBuffer(firstTrack: 1, lastTrack: 6, trackAbsolutes: CanonicalTrackOffsets, leadOutAbsolute: CanonicalLeadOut);
        DiscToc toc = WindowsTocReader.ParseCdromToc(tocBuffer: buffer);

        toc.TrackOffsetsSectors[trackIndex].Should().Be(expected: expectedOffset);
    }

    // ── ParseCdromToc — error cases ───────────────────────────────────────────

    [Fact]
    public void ParseCdromToc_ShortBuffer_ThrowsArgumentException()
    {
        byte[] shortBuffer = new byte[100];

        Action act = () => WindowsTocReader.ParseCdromToc(tocBuffer: shortBuffer);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(expectedWildcardPattern: "*CDROM_TOC buffer must be at least 804 bytes*");
    }

    [Fact]
    public void ParseCdromToc_EmptyBuffer_ThrowsArgumentException()
    {
        byte[] emptyBuffer = [];

        Action act = () => WindowsTocReader.ParseCdromToc(tocBuffer: emptyBuffer);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ParseCdromToc_BufferWithoutLeadOut_ThrowsInvalidOperationException()
    {
        // Build a buffer with no 0xAA entry (all track entries zero).
        byte[] buffer = new byte[804];
        buffer[2] = 1;
        buffer[3] = 2;

        Action act = () => WindowsTocReader.ParseCdromToc(tocBuffer: buffer);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*lead-out*");
    }

    // ── ReadTocAsync — non-Windows returns null ───────────────────────────────

    [Fact]
    public async Task ReadTocAsync_NonWindows_ReturnsNull()
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows the method proceeds to the drive-open path; we
            // cannot test the non-Windows branch from a Windows host.
            // This case is verified structurally: the guard is the first
            // statement in ReadTocAsync.
            return;
        }

        WindowsTocReader reader = new();
        DiscToc? result = await reader.ReadTocAsync(drivePath: "D:\\", ct: CancellationToken.None);

        result.Should().BeNull(because: "non-Windows host must short-circuit before any I/O");
    }

    // ── ReadTocAsync — invalid drive path returns null ────────────────────────

    [Fact]
    public async Task ReadTocAsync_InvalidDrivePath_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // A path that cannot be parsed into a drive letter must not throw.
        WindowsTocReader reader = new();
        DiscToc? result = await reader.ReadTocAsync(drivePath: "/dev/sr0", ct: CancellationToken.None);

        result.Should().BeNull(because: "unresolvable drive path must return null, not throw");
    }

    [Fact]
    public async Task ReadTocAsync_ValidNonOpticalDriveLetter_ReturnsNullWithoutThrowing()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // hardware-validate: exercises the real CreateFileW / DeviceIoControl
        // P/Invoke path against a genuine, always-present drive letter (C:) —
        // itemized rather than faked (no seam over kernel32 P/Invokes). C: is
        // never a CD-ROM, so this always resolves to a real failure inside the
        // Windows I/O path (either CreateFileW returning an invalid handle
        // because raw volume access needs elevation, or a valid handle whose
        // IOCTL_CDROM_READ_TOC subsequently fails because the device isn't an
        // optical drive) — either way it proves the non-throwing null-return
        // contract end to end without needing a physical CD-ROM.
        WindowsTocReader reader = new();
        DiscToc? result = await reader.ReadTocAsync(drivePath: "C:\\", ct: CancellationToken.None);

        result.Should().BeNull(because: "C: is never a CD-ROM device");
    }

    [Fact]
    public async Task ReadTocAsync_RealOpticalDriveLetter_DoesNotThrow_ProbeExactBehavior()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Probe test: this dev machine currently has a real disc in D:.
        // Whatever CreateFileW/DeviceIoControl actually do against a genuine
        // optical drive (succeed with a real TOC, fail on a non-audio
        // disc/handle permissions, or throw) is accepted here — the only
        // hard requirement is the documented never-throw contract. This
        // exercises the CreateFileW/DeviceIoControl P/Invoke path against a
        // real optical device rather than a non-CD-ROM drive letter.
        WindowsTocReader reader = new();

        Func<Task<DiscToc?>> act = () => reader.ReadTocAsync(drivePath: "D:\\", ct: CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadTocAsync_BareDriveLetterWithoutColon_ResolvesAndReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // ResolveDriveLetter's single-character branch (drivePath.Length == 1).
        WindowsTocReader reader = new();
        DiscToc? result = await reader.ReadTocAsync(drivePath: "C", ct: CancellationToken.None);

        result.Should().BeNull();
    }

    // ── MSF round-trip — spot checks ─────────────────────────────────────────

    [Theory]
    [InlineData(data: [150, 0, 2, 0])] // track 1 canonical: 00:02:00
    [InlineData(data: [15363, 3, 24, 63])] // track 2 canonical: 03:24:63
    [InlineData(data: [95462, 21, 12, 62])] // lead-out canonical: 21:12:62
    public void AbsoluteToMsf_SpotChecks(
        int absolute,
        int expectedMinute,
        int expectedSecond,
        int expectedFrame
    )
    {
        (byte minute, byte second, byte frame) = AbsoluteToMsf(absolute: absolute);

        minute.Should().Be(expected: (byte)expectedMinute);
        second.Should().Be(expected: (byte)expectedSecond);
        frame.Should().Be(expected: (byte)expectedFrame);
    }

    // ── MsfToAbsolute inverse (via ParseCdromToc) ─────────────────────────────

    [Fact]
    public void ParseCdromToc_SingleTrackDisc_CorrectOffsets()
    {
        int[] singleTrack = [150];
        int leadOut = 18150;
        byte[] buffer = BuildTocBuffer(firstTrack: 1, lastTrack: 1, trackAbsolutes: singleTrack, leadOutAbsolute: leadOut);

        DiscToc toc = WindowsTocReader.ParseCdromToc(tocBuffer: buffer);

        toc.FirstTrack.Should().Be(expected: 1);
        toc.LastTrack.Should().Be(expected: 1);
        toc.TrackOffsetsSectors.Should().Equal(elements: [150]);
        toc.LeadOutOffsetSectors.Should().Be(expected: leadOut);
    }
}
