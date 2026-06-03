using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Windows <see cref="ITocReader"/> that issues <c>IOCTL_CDROM_READ_TOC</c>
/// (0x00024000) via <c>DeviceIoControl</c> and parses the raw
/// <c>CDROM_TOC</c> structure (804 bytes) into a <see cref="DiscToc"/>.
///
/// Returns null on any failure (non-Windows platform detected at runtime,
/// no disc inserted, or IOCTL error), causing the caller to degrade to
/// <see cref="DiscIdentification.NeedsManualAssignment"/>.
///
/// The public <see cref="ReadTocAsync"/> method contains all I/O.
/// The pure <see cref="ParseCdromToc"/> method is the unit-testable kernel —
/// it accepts a raw byte array and performs no I/O.
/// </summary>
public sealed class WindowsTocReader : ITocReader
{
    // ── CDROM_TOC layout constants ────────────────────────────────────────────

    private const int TocBufferSize = 804;
    private const int TrackDataOffset = 4;
    private const int TrackDataSize = 8;
    private const int MaxTrackEntries = 100;
    private const byte LeadOutTrackNumber = 0xAA;

    // TRACK_DATA byte indices (relative to the start of each 8-byte entry).
    private const int TdReserved = 0;
    private const int TdAdrControl = 1;
    private const int TdTrackNumber = 2;
    private const int TdReserved1 = 3;
    private const int TdAddr0 = 4;
    private const int TdMinute = 5;
    private const int TdSecond = 6;
    private const int TdFrame = 7;

    // ── IOCTL constants ───────────────────────────────────────────────────────

    private const uint GenericRead = 0x80000000u;
    private const uint FileShareReadWrite = 0x3u;
    private const uint OpenExisting = 3u;
    private const uint IoctlCdromReadToc = 0x00024000u;

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [SupportedOSPlatform("windows")]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    [SupportedOSPlatform("windows")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        byte[] lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped
    );

    // ── ITocReader implementation ─────────────────────────────────────────────

    // hardware-validate: DeviceIoControl path needs a real audio CD
    public Task<DiscToc?> ReadTocAsync(string drivePath, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult<DiscToc?>(null);

        try
        {
            char driveLetter = ResolveDriveLetter(drivePath);
            string devicePath = $@"\\.\{driveLetter}:";

            using SafeFileHandle handle = CreateFileW(
                devicePath,
                GenericRead,
                FileShareReadWrite,
                IntPtr.Zero,
                OpenExisting,
                0u,
                IntPtr.Zero
            );

            if (handle.IsInvalid)
                return Task.FromResult<DiscToc?>(null);

            byte[] buffer = new byte[TocBufferSize];
            bool ok = DeviceIoControl(
                handle,
                IoctlCdromReadToc,
                IntPtr.Zero,
                0u,
                buffer,
                TocBufferSize,
                out uint _,
                IntPtr.Zero
            );

            if (!ok)
                return Task.FromResult<DiscToc?>(null);

            DiscToc toc = ParseCdromToc(buffer);
            return Task.FromResult<DiscToc?>(toc);
        }
        catch
        {
            return Task.FromResult<DiscToc?>(null);
        }
    }

    // ── Pure parsing kernel ───────────────────────────────────────────────────

    /// <summary>
    /// Parses a raw <c>CDROM_TOC</c> byte buffer (804 bytes) into a
    /// <see cref="DiscToc"/> with absolute frame offsets.
    ///
    /// The absolute frame offset for each track is computed from its MSF
    /// address as: <c>(Minute*60 + Second)*75 + Frame</c>. Track 1 is normally
    /// at MSF 00:02:00 = absolute frame 150. No 150-frame adjustment is applied
    /// here — the MSF values on disc already encode the absolute offset, and
    /// <see cref="MusicBrainzDiscId.Compute"/> hashes them as-is.
    ///
    /// This method is pure (no I/O) and is the unit-testable entry point.
    /// </summary>
    /// <param name="tocBuffer">
    /// Exactly 804-byte <c>CDROM_TOC</c> buffer as returned by
    /// <c>IOCTL_CDROM_READ_TOC</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the buffer is too short to contain a valid <c>CDROM_TOC</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the lead-out track (0xAA) is absent from the buffer.
    /// </exception>
    internal static DiscToc ParseCdromToc(byte[] tocBuffer)
    {
        if (tocBuffer.Length < TocBufferSize)
        {
            throw new ArgumentException(
                $"CDROM_TOC buffer must be at least {TocBufferSize} bytes; got {tocBuffer.Length}.",
                nameof(tocBuffer)
            );
        }

        byte firstTrack = tocBuffer[2];
        byte lastTrack = tocBuffer[3];

        int? leadOutAbsolute = null;
        int trackCount = lastTrack - firstTrack + 1;
        int[] trackOffsets = new int[trackCount];

        for (int entryIndex = 0; entryIndex < MaxTrackEntries; entryIndex++)
        {
            int entryStart = TrackDataOffset + entryIndex * TrackDataSize;
            byte trackNumber = tocBuffer[entryStart + TdTrackNumber];

            if (trackNumber == LeadOutTrackNumber)
            {
                leadOutAbsolute = MsfToAbsolute(
                    tocBuffer[entryStart + TdMinute],
                    tocBuffer[entryStart + TdSecond],
                    tocBuffer[entryStart + TdFrame]
                );
                continue;
            }

            int slotIndex = trackNumber - firstTrack;
            if (slotIndex >= 0 && slotIndex < trackCount)
            {
                trackOffsets[slotIndex] = MsfToAbsolute(
                    tocBuffer[entryStart + TdMinute],
                    tocBuffer[entryStart + TdSecond],
                    tocBuffer[entryStart + TdFrame]
                );
            }
        }

        if (leadOutAbsolute is null)
        {
            throw new InvalidOperationException(
                "CDROM_TOC buffer does not contain a lead-out track entry (0xAA)."
            );
        }

        return new DiscToc(firstTrack, lastTrack, leadOutAbsolute.Value, trackOffsets);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int MsfToAbsolute(byte minute, byte second, byte frame) =>
        (minute * 60 + second) * 75 + frame;

    private static char ResolveDriveLetter(string drivePath)
    {
        string trimmed = drivePath.TrimEnd('\\', '/');
        if (trimmed.Length >= 2 && trimmed[1] == ':' && char.IsLetter(trimmed[0]))
            return char.ToUpperInvariant(trimmed[0]);

        if (trimmed.Length == 1 && char.IsLetter(trimmed[0]))
            return char.ToUpperInvariant(trimmed[0]);

        throw new ArgumentException(
            $"Cannot extract a drive letter from path '{drivePath}'.",
            nameof(drivePath)
        );
    }
}
