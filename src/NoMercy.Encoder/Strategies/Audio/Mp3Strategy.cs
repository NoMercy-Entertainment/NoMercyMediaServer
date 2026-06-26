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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Strategies.Audio;

/// <summary>
/// MP3 single-file output. Audio codecs don't meaningfully benefit from
/// 2-pass — there's no MP3 two-pass strategy, just this single-pass one.
/// </summary>
public class Mp3Strategy(IEncoder encoder) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.Mp3;
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    ) => encoder.EncodeAsync(request, progress, ct);
}
