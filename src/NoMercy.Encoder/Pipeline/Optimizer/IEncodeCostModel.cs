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

namespace NoMercy.Encoder.Pipeline.Optimizer;

/// <summary>Output pixels + encoder speed → relative (unitless) encode cost.</summary>
public record CostRung(int Width, int Height, VideoCodecType Codec, string Encoder, int Passes);

public interface IEncodeCostModel
{
    double RungCost(int width, int height, VideoCodecType codec, string encoder, int passes);
    double TotalCost(int sourceWidth, int sourceHeight, bool sourceIsHdr, CostRung[] rungs);
}
