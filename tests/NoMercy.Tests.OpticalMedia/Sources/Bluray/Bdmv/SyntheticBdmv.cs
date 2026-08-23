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

using System.Buffers.Binary;
using System.Text;

namespace NoMercy.DiscFormat.Tests;

/// Hand-builds spec-shaped BD-ROM structures so the front-end parsers can be exercised without a real
/// disc (the repo holds only extracted JARs and recovered JSON, never raw index.bdmv / MPLS / BDJO).
/// Each builder writes the exact big-endian fields at the offsets libbluray reads, so these are true
/// round-trip fixtures, not mocks. A real disc would feed the same parsers the same way.
internal static class SyntheticBdmv
{
    private const double TicksPerSecond = 45000.0;

    /// One title-table entry's object: either an HDMV MovieObject id or a 5-character BD-J reference.
    internal sealed record IndexEntry
    {
        public required bool IsBdj { get; init; }
        public string? BdjoName { get; init; }
        public int HdmvId { get; init; }
    }

    /// Builds an index.bdmv: "INDX" + version, a u32 Indexes() start at byte 8, then the length word,
    /// the FirstPlayback object, the TopMenu object, the title count, and one 12-byte entry per title.
    public static byte[] IndexBdmv(
        IndexEntry firstPlayback,
        IndexEntry topMenu,
        IReadOnlyList<IndexEntry> titles
    )
    {
        const int indexStart = 40;
        int size = indexStart + 4 + 12 + 12 + 2 + (titles.Count * 12);
        byte[] image = new byte[size];

        WriteAscii(image, 0, "INDX");
        WriteAscii(image, 4, "0200");
        WriteU32(image, 8, indexStart);

        int cursor = indexStart;
        WriteU32(image, cursor, (uint)(size - indexStart - 4)); // index length (informational)
        cursor += 4;
        WriteObject(image, cursor, firstPlayback);
        cursor += 12;
        WriteObject(image, cursor, topMenu);
        cursor += 12;
        WriteU16(image, cursor, (ushort)titles.Count);
        cursor += 2;
        foreach (IndexEntry title in titles)
        {
            WriteObject(image, cursor, title);
            cursor += 12;
        }

        return image;
    }

    private static void WriteObject(byte[] image, int offset, IndexEntry entry)
    {
        // First byte top two bits = object_type (1 = HDMV, 2 = BD-J).
        if (entry.IsBdj)
        {
            image[offset] = 0x2 << 6;
            // BD-J object: playback_type(2) + 14 skip then name[5] at byte 6.
            WriteAscii(image, offset + 6, entry.BdjoName ?? "00000");
        }
        else
        {
            image[offset] = 0x1 << 6;
            WriteU16(image, offset + 6, (ushort)entry.HdmvId);
        }
    }

    /// Builds a BDJO blob: the parser scans plaintext, so a minimal blob that names an Xlet class and
    /// embeds the accessible playlist ids (as isolated 5-digit tokens) is enough to exercise it. The
    /// surrounding bytes mimic the format's binary padding without needing a full section walk.
    public static byte[] Bdjo(string? xletClass, IReadOnlyList<string> playlistTokens)
    {
        List<byte> blob = [];
        blob.AddRange(Encoding.ASCII.GetBytes("BDJO0200"));
        blob.AddRange([0x00, 0x00, 0x00, 0x00]);
        if (xletClass is not null)
        {
            blob.AddRange(Encoding.ASCII.GetBytes(xletClass));
            blob.Add(0x00);
        }

        foreach (string token in playlistTokens)
        {
            blob.AddRange([0x00, 0x00]);
            blob.AddRange(Encoding.ASCII.GetBytes(token));
            blob.AddRange([0x00, 0x00]);
        }

        return blob.ToArray();
    }

    /// One PlayItem definition: a clip, its duration in seconds and the marks (in seconds, relative to
    /// the playlist start) that fall inside it.
    internal sealed record PlayItemDef
    {
        public required string Clip { get; init; }
        public required double DurationSeconds { get; init; }
    }

    /// Builds an MPLS playlist with the given PlayItems and PlayListMarks (each mark a playlist-second
    /// resolved against the PlayItems), plus a single-PlayItem STN table when streams are supplied.
    /// PlayItem IN time is fixed at zero so a mark second is its raw timestamp.
    public static byte[] Mpls(
        IReadOnlyList<PlayItemDef> playItems,
        IReadOnlyList<double> markSeconds,
        IReadOnlyList<(int Coding, string Language)>? audio = null,
        IReadOnlyList<(int Coding, string Language)>? subtitles = null,
        IReadOnlyList<(int Coding, string Language)>? interactive = null
    )
    {
        byte[] playListSection = BuildPlayListSection(
            playItems,
            audio ?? [],
            subtitles ?? [],
            interactive ?? []
        );
        byte[] markSection = BuildMarkSection(playItems, markSeconds);

        const int headerSize = 40; // "MPLS" + version + offsets + reserved padding
        int playListStart = headerSize;
        int markStart = playListStart + playListSection.Length;
        byte[] image = new byte[markStart + markSection.Length];

        WriteAscii(image, 0, "MPLS");
        WriteAscii(image, 4, "0200");
        WriteU32(image, 0x08, (uint)playListStart);
        WriteU32(image, 0x0C, (uint)markStart);
        playListSection.CopyTo(image.AsSpan(playListStart));
        markSection.CopyTo(image.AsSpan(markStart));
        return image;
    }

    private static byte[] BuildPlayListSection(
        IReadOnlyList<PlayItemDef> playItems,
        IReadOnlyList<(int Coding, string Language)> audio,
        IReadOnlyList<(int Coding, string Language)> subtitles,
        IReadOnlyList<(int Coding, string Language)> interactive
    )
    {
        List<byte> body = [];
        // reserved u16 + number_of_PlayItems u16 + number_of_SubPaths u16
        body.AddRange([0x00, 0x00]);
        body.AddRange(U16Bytes((ushort)playItems.Count));
        body.AddRange([0x00, 0x00]);

        for (int index = 0; index < playItems.Count; index++)
        {
            byte[] entry = BuildPlayItem(
                playItems[index],
                index == 0 ? audio : [],
                index == 0 ? subtitles : [],
                index == 0 ? interactive : []
            );
            body.AddRange(U16Bytes((ushort)entry.Length));
            body.AddRange(entry);
        }

        List<byte> section = [];
        section.AddRange(U32Bytes((uint)body.Count));
        section.AddRange(body);
        return section.ToArray();
    }

    private static byte[] BuildPlayItem(
        PlayItemDef item,
        IReadOnlyList<(int Coding, string Language)> audio,
        IReadOnlyList<(int Coding, string Language)> subtitles,
        IReadOnlyList<(int Coding, string Language)> interactive
    )
    {
        List<byte> entry = [];
        entry.AddRange(Encoding.ASCII.GetBytes(item.Clip.PadRight(5)[..5])); // clip_id[5]
        entry.AddRange(Encoding.ASCII.GetBytes("M2TS")); // codec[4]
        entry.AddRange([0x00, 0x00]); // flags (single-angle)
        entry.Add(0x00); // ref_to_STC_id
        entry.AddRange(U32Bytes(0)); // IN_time
        entry.AddRange(U32Bytes((uint)(item.DurationSeconds * TicksPerSecond))); // OUT_time
        entry.AddRange(new byte[8]); // UO_mask
        entry.Add(0x00); // random access + reserved
        entry.Add(0x00); // still_mode
        entry.AddRange([0x00, 0x00]); // still_time

        entry.AddRange(BuildStn(audio, subtitles, interactive));
        return entry.ToArray();
    }

    /// A multi-angle PlayItem: the same fixed header with the multi-angle flag set, then num_angles, a
    /// flags byte and one clip/codec/stc block per extra angle. Used to prove angle decoding.
    public static byte[] MplsMultiAngle(
        string primaryClip,
        IReadOnlyList<string> extraAngleClips,
        double durationSeconds
    )
    {
        List<byte> entry = [];
        entry.AddRange(Encoding.ASCII.GetBytes(primaryClip.PadRight(5)[..5]));
        entry.AddRange(Encoding.ASCII.GetBytes("M2TS"));
        entry.AddRange(U16Bytes(0x0010)); // bit 4 = is_multi_angle
        entry.Add(0x00);
        entry.AddRange(U32Bytes(0));
        entry.AddRange(U32Bytes((uint)(durationSeconds * TicksPerSecond)));
        entry.AddRange(new byte[8]);
        entry.Add(0x00);
        entry.Add(0x00);
        entry.AddRange([0x00, 0x00]);
        entry.Add((byte)(extraAngleClips.Count + 1)); // number_of_angles
        entry.Add(0x00); // angle flags
        foreach (string clip in extraAngleClips)
        {
            entry.AddRange(Encoding.ASCII.GetBytes(clip.PadRight(5)[..5]));
            entry.AddRange(Encoding.ASCII.GetBytes("M2TS"));
            entry.Add(0x00);
        }

        entry.AddRange(BuildStn([], [], []));

        List<byte> body = [];
        body.AddRange([0x00, 0x00]);
        body.AddRange(U16Bytes(1));
        body.AddRange([0x00, 0x00]);
        body.AddRange(U16Bytes((ushort)entry.Count));
        body.AddRange(entry);

        List<byte> playListSection = [];
        playListSection.AddRange(U32Bytes((uint)body.Count));
        playListSection.AddRange(body);

        byte[] markSection = BuildMarkSection(
            [new PlayItemDef { Clip = primaryClip, DurationSeconds = durationSeconds }],
            [0]
        );

        const int headerSize = 40;
        int playListStart = headerSize;
        int markStart = playListStart + playListSection.Count;
        byte[] image = new byte[markStart + markSection.Length];
        WriteAscii(image, 0, "MPLS");
        WriteAscii(image, 4, "0200");
        WriteU32(image, 0x08, (uint)playListStart);
        WriteU32(image, 0x0C, (uint)markStart);
        playListSection.ToArray().CopyTo(image.AsSpan(playListStart));
        markSection.CopyTo(image.AsSpan(markStart));
        return image;
    }

    private static byte[] BuildStn(
        IReadOnlyList<(int Coding, string Language)> audio,
        IReadOnlyList<(int Coding, string Language)> subtitles,
        IReadOnlyList<(int Coding, string Language)> interactive
    )
    {
        List<byte> streams = [];
        foreach ((int coding, string language) in audio)
        {
            streams.AddRange(AudioStream(coding, language));
        }

        foreach ((int coding, string language) in subtitles)
        {
            streams.AddRange(
                coding == 0x92 ? TextStream(coding, language) : PgStream(coding, language)
            );
        }

        // IG entries share the PG entry shape and sit after the PG (+ PiP PG) block on the wire.
        foreach ((int coding, string language) in interactive)
        {
            streams.AddRange(PgStream(coding, language));
        }

        List<byte> stn = [];
        // length u16 (filled below) + reserved u16
        stn.AddRange([0x00, 0x00, 0x00, 0x00]);
        stn.Add(0x00); // n_video
        stn.Add((byte)audio.Count);
        stn.Add((byte)subtitles.Count);
        stn.Add((byte)interactive.Count); // n_ig
        stn.AddRange([0x00, 0x00, 0x00]); // n_sec_audio, n_sec_video, n_pip
        stn.AddRange(new byte[5]); // 5 reserved
        stn.AddRange(streams);

        ushort length = (ushort)(stn.Count - 2);
        stn[0] = (byte)(length >> 8);
        stn[1] = (byte)(length & 0xFF);
        return stn.ToArray();
    }

    private static byte[] AudioStream(int coding, string language)
    {
        byte[] entry = [0x01, 0x00]; // stream_entry: length 1 + one ref byte
        // attributes: length, coding, format/rate byte, language[3]
        byte[] languageBytes = Encoding.ASCII.GetBytes(language.PadRight(3)[..3]);
        byte[] attributes =
        [
            0x05,
            (byte)coding,
            0x00,
            languageBytes[0],
            languageBytes[1],
            languageBytes[2],
        ];
        return [.. entry, .. attributes];
    }

    private static byte[] PgStream(int coding, string language)
    {
        byte[] entry = [0x01, 0x00];
        byte[] languageBytes = Encoding.ASCII.GetBytes(language.PadRight(3)[..3]);
        byte[] attributes =
        [
            0x04,
            (byte)coding,
            languageBytes[0],
            languageBytes[1],
            languageBytes[2],
        ];
        return [.. entry, .. attributes];
    }

    /// TextST (0x92) attributes carry a character-code byte between the coding type and the
    /// language, unlike PG/IG (mpls_parse.c / clpi_parse.c case 0x92).
    private static byte[] TextStream(int coding, string language)
    {
        byte[] entry = [0x01, 0x00];
        byte[] languageBytes = Encoding.ASCII.GetBytes(language.PadRight(3)[..3]);
        byte[] attributes =
        [
            0x05,
            (byte)coding,
            0x12,
            languageBytes[0],
            languageBytes[1],
            languageBytes[2],
        ];
        return [.. entry, .. attributes];
    }

    private static byte[] BuildMarkSection(
        IReadOnlyList<PlayItemDef> playItems,
        IReadOnlyList<double> markSeconds
    )
    {
        long[] cumulative = new long[playItems.Count];
        long accumulated = 0;
        for (int index = 0; index < playItems.Count; index++)
        {
            cumulative[index] = accumulated;
            accumulated += (long)(playItems[index].DurationSeconds * TicksPerSecond);
        }

        List<byte> body = [];
        body.AddRange(U16Bytes((ushort)markSeconds.Count));
        foreach (double second in markSeconds)
        {
            long playlistTicks = (long)(second * TicksPerSecond);
            int playItemRef = PlayItemFor(cumulative, playlistTicks);
            long timestamp = playlistTicks - cumulative[playItemRef];

            body.Add(0x00); // reserved
            body.Add(0x01); // mark_type
            body.AddRange(U16Bytes((ushort)playItemRef));
            body.AddRange(U32Bytes((uint)timestamp));
            body.AddRange([0x00, 0x00]); // entry_ES_PID
            body.AddRange(U32Bytes(0)); // duration
        }

        List<byte> section = [];
        section.AddRange(U32Bytes((uint)body.Count));
        section.AddRange(body);
        return section.ToArray();
    }

    private static int PlayItemFor(long[] cumulative, long playlistTicks)
    {
        for (int index = cumulative.Length - 1; index >= 0; index--)
        {
            if (playlistTicks >= cumulative[index])
            {
                return index;
            }
        }

        return 0;
    }

    private static void WriteAscii(byte[] image, int offset, string value)
    {
        Encoding.ASCII.GetBytes(value).CopyTo(image.AsSpan(offset));
    }

    private static void WriteU16(byte[] image, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(offset, sizeof(ushort)), value);
    }

    private static void WriteU32(byte[] image, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(offset, sizeof(uint)), value);
    }

    private static byte[] U16Bytes(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] U32Bytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
