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

namespace NoMercy.Encoder.Naming;

public class MediaKeyResolver : IMediaKeyResolver
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    public string ForMedia(MediaType type, long id)
    {
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Media id must be non-negative.");

        char prefix = type switch
        {
            MediaType.Movie => 'm',
            MediaType.Episode => 'e',
            MediaType.Track => 't',
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        return $"{prefix}{ToBase36(id)}";
    }

    private static string ToBase36(long value)
    {
        if (value == 0)
            return "0";

        Span<char> buffer = stackalloc char[13]; // long.MaxValue in base36 = 13 digits
        int i = buffer.Length;
        while (value > 0)
        {
            buffer[--i] = Alphabet[(int)(value % 36)];
            value /= 36;
        }
        return new string(buffer[i..]);
    }
}
