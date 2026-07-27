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

namespace NoMercy.Encoder.PostProcess;

/// <summary>
/// The tile layout a sprite sheet is pinned to, and the frame count that fills
/// it exactly.
///
/// <para>Left to itself the muxer sizes the grid from however many frames it
/// received and leaves the tail of the last row untouched. Untouched is not
/// blank: the canvas is YUV, and its zeroed chroma is saturated green, so every
/// sheet whose frames did not land on a whole grid ends in a green block
/// (nomercy-ffmpeg#40). Nothing here can colour those cells — but a grid with no
/// empty cells has none to colour, and that is reachable from the caller.</para>
///
/// <para>So the columns are stated rather than inferred, and the frame stream is
/// padded with real black frames and then cut to exactly <see cref="CellCount"/>.
/// The frame count is deliberately over-estimated: too many means a few black
/// tiles past the end of the film, too few would cut real ones off it.</para>
/// </summary>
public readonly record struct SpriteGrid(int Columns, int Rows)
{
    /// <summary>Frames needed to fill the grid with nothing left over.</summary>
    public int CellCount => Columns * Rows;

    /// <summary>
    /// Layout for a title of <paramref name="duration"/> sampled every
    /// <paramref name="intervalSeconds"/>.
    /// </summary>
    public static SpriteGrid For(TimeSpan duration, int intervalSeconds)
    {
        if (intervalSeconds <= 0)
            intervalSeconds = 1;

        // What the fps filter emits is not quite predictable at the boundaries —
        // an 80-second title sampled every 10 gives 8 frames, not 9, because the
        // frame at 80s is past the last one that exists. Rounding down and then
        // adding a margin keeps the estimate on the safe side of that, which is
        // the side where the surplus is black tiles instead of lost film.
        int frames = (int)Math.Floor(duration.TotalSeconds / intervalSeconds) + 2;
        if (frames < 1)
            frames = 1;

        int columns = (int)Math.Ceiling(Math.Sqrt(frames));
        int rows = (int)Math.Ceiling((double)frames / columns);

        return new(columns, rows);
    }
}
