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

namespace NoMercy.DiscFormat.Disc.Bdmv;

/// Parses a BDAV MPLS playlist into its PlayItems, PlayListMarks (resolved to presentation seconds)
/// and the first PlayItem's STN table. Faithful to libbluray mpls_parse.c:
///   header "MPLS" + version, a u32 PlayList start at 0x08 and a u32 PlayListMark start at 0x0C.
///   Each PlayItem is u16-length-prefixed: clip_id[5], codec[4], a flags word whose bit 4 is
///   is_multi_angle, ref_to_STC_id, IN_time u32, OUT_time u32, then UO/still fields; a multi-angle
///   PlayItem appends a fixed header per spec angle.
///   A PlayListMark is 14 bytes: type, ref_to_PlayItem, a 45 kHz timestamp relative to that
///   PlayItem's IN_time. The mark's presentation time is the sum of preceding PlayItem durations
///   plus (timestamp - IN_time of the referenced PlayItem).
public static class MplsParser
{
    private const string Magic = "MPLS";
    private const double TicksPerSecond = 45000.0;
    private const int PlayListStartOffset = 0x08;
    private const int PlayListMarkStartOffset = 0x0C;
    private const int MarkRecordSize = 14;

    public static MplsPlaylist Parse(ReadOnlySpan<byte> data)
    {
        if (BigEndianReader.Ascii(data, 0, Magic.Length) != Magic)
        {
            throw new InvalidDataException("playlist does not start with the MPLS signature.");
        }

        int playListStart = (int)BigEndianReader.U32(data, PlayListStartOffset);
        int markStart = (int)BigEndianReader.U32(data, PlayListMarkStartOffset);

        IReadOnlyList<MplsPlayItem> playItems = ParsePlayItems(
            data,
            playListStart,
            out int firstStnOffset
        );
        IReadOnlyList<MplsMark> marks = ParseMarks(
            data,
            markStart,
            playItems,
            out double totalSeconds
        );
        ParseStn(
            data,
            firstStnOffset,
            out List<MplsStream> audio,
            out List<MplsStream> subtitles,
            out List<MplsStream> interactive
        );

        return new MplsPlaylist
        {
            PlayItems = playItems,
            Marks = marks,
            AudioStreams = audio,
            SubtitleStreams = subtitles,
            InteractiveGraphicsStreams = interactive,
            TotalSeconds = totalSeconds,
        };
    }

    private static List<MplsPlayItem> ParsePlayItems(
        ReadOnlySpan<byte> data,
        int playListStart,
        out int firstStnOffset
    )
    {
        int numPlayItems = BigEndianReader.U16(data, playListStart + 6);
        int cursor = playListStart + 10;
        List<MplsPlayItem> items = new(numPlayItems);
        firstStnOffset = -1;

        for (int index = 0; index < numPlayItems; index++)
        {
            int entryLength = BigEndianReader.U16(data, cursor);
            int entry = cursor + 2;

            string clip = BigEndianReader.Ascii(data, entry, 5);
            int flags = BigEndianReader.U16(data, entry + 9);
            bool isMultiAngle = ((flags >> 4) & 0x1) == 1;
            long inTime = BigEndianReader.U32(data, entry + 12);
            long outTime = BigEndianReader.U32(data, entry + 16);

            // Fixed PlayItem header to still_time end: clip(5)+codec(4)+flags(2)+stc(1)+in(4)+out(4)
            //   +UO(8)+random_access(1)+still_mode(1)+still_time(2) = 32 bytes.
            int afterFixed = entry + 32;
            int angleCount = 1;
            List<string> angleClips = [];
            if (isMultiAngle)
            {
                angleCount = BigEndianReader.U8(data, afterFixed);
                int anglesCursor = afterFixed + 2; // num_angles + the audio/seamless flags byte
                for (int angle = 0; angle < angleCount - 1; angle++)
                {
                    angleClips.Add(BigEndianReader.Ascii(data, anglesCursor, 5));
                    anglesCursor += 10; // clip(5)+codec(4)+stc(1)
                }

                afterFixed = anglesCursor;
            }

            if (firstStnOffset < 0)
            {
                firstStnOffset = afterFixed;
            }

            items.Add(
                new MplsPlayItem
                {
                    Index = index,
                    ClipName = clip,
                    InTime = inTime,
                    OutTime = outTime,
                    IsMultiAngle = isMultiAngle,
                    AngleCount = angleCount,
                    AngleClips = angleClips,
                }
            );

            cursor = entry + entryLength;
        }

        return items;
    }

    private static List<MplsMark> ParseMarks(
        ReadOnlySpan<byte> data,
        int markStart,
        IReadOnlyList<MplsPlayItem> items,
        out double totalSeconds
    )
    {
        long[] cumulative = new long[items.Count];
        long accumulated = 0;
        for (int index = 0; index < items.Count; index++)
        {
            cumulative[index] = accumulated;
            accumulated += items[index].Duration;
        }

        totalSeconds = Round(accumulated / TicksPerSecond);

        int numMarks = BigEndianReader.U16(data, markStart + 4);
        int cursor = markStart + 6;
        List<MplsMark> marks = new(numMarks);

        for (int index = 0; index < numMarks; index++)
        {
            int markType = BigEndianReader.U8(data, cursor + 1);
            int refPlayItem = BigEndianReader.U16(data, cursor + 2);
            long timestamp = BigEndianReader.U32(data, cursor + 4);

            long inTime = refPlayItem < items.Count ? items[refPlayItem].InTime : 0;
            long playlistTicks =
                refPlayItem < items.Count
                    ? cumulative[refPlayItem] + (timestamp - inTime)
                    : timestamp;

            marks.Add(
                new MplsMark
                {
                    Index = index,
                    MarkType = markType,
                    PlayItemRef = refPlayItem,
                    Seconds = Round(playlistTicks / TicksPerSecond),
                }
            );

            cursor += MarkRecordSize;
        }

        return marks;
    }

    /// The STN_table of the first PlayItem: a length word, reserved word, then stream counts, then the
    /// stream entry/attribute pairs. Audio streams carry their ISO-639-2 language three bytes into the
    /// attribute block; PG (subtitle) streams carry it right after the coding type.
    private static void ParseStn(
        ReadOnlySpan<byte> data,
        int stnOffset,
        out List<MplsStream> audio,
        out List<MplsStream> subtitles,
        out List<MplsStream> interactive
    )
    {
        int cursor = stnOffset + 4; // skip length u16 + reserved u16
        int numVideo = BigEndianReader.U8(data, cursor);
        int numAudio = BigEndianReader.U8(data, cursor + 1);
        int numPg = BigEndianReader.U8(data, cursor + 2);
        int numIg = BigEndianReader.U8(data, cursor + 3);
        int numPipPg = BigEndianReader.U8(data, cursor + 6);
        cursor += 7 + 5; // 7 count bytes + 5 reserved

        ReadStreams(data, ref cursor, numVideo);
        audio = ReadStreams(data, ref cursor, numAudio);
        subtitles = ReadStreams(data, ref cursor, numPg);
        // PiP PG entries sit between the primary PG block and the IG block on the wire.
        ReadStreams(data, ref cursor, numPipPg);
        interactive = ReadStreams(data, ref cursor, numIg);
    }

    private static List<MplsStream> ReadStreams(ReadOnlySpan<byte> data, ref int cursor, int count)
    {
        List<MplsStream> streams = new(count);
        for (int index = 0; index < count; index++)
        {
            // stream_entry: u8 length then type-specific ref ids.
            int entryLength = BigEndianReader.U8(data, cursor);
            cursor += 1 + entryLength;

            // stream_attributes: u8 length then coding type and coding-specific fields.
            int attributesLength = BigEndianReader.U8(data, cursor);
            int coding = BigEndianReader.U8(data, cursor + 1);
            CodingTypeInfo? info = CodingType.Resolve(coding);
            string? language = ReadLanguage(data, cursor, info);
            streams.Add(
                new MplsStream
                {
                    CodingType = coding,
                    Codec = info?.Name,
                    Language = language,
                }
            );
            cursor += 1 + attributesLength;
        }

        return streams;
    }

    private static string? ReadLanguage(
        ReadOnlySpan<byte> data,
        int attributesOffset,
        CodingTypeInfo? info
    )
    {
        return info?.Kind switch
        {
            // audio: (format<<4 | sample_rate) byte, then language_code[3].
            CodingKind.Audio => Language(data, attributesOffset + 3),
            // PG/IG: language_code[3] right after the coding type.
            CodingKind.Graphics => Language(data, attributesOffset + 2),
            // TextST: a character-code byte sits between coding type and language_code[3].
            CodingKind.Text => Language(data, attributesOffset + 3),
            _ => null,
        };
    }

    private static string? Language(ReadOnlySpan<byte> data, int offset)
    {
        string code = BigEndianReader.Ascii(data, offset, 3);
        return string.IsNullOrEmpty(code) ? null : code;
    }

    private static double Round(double value)
    {
        return Math.Round(value, 3);
    }
}
