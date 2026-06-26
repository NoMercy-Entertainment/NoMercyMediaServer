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

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Discriminated union describing how a video output controls bitrate /
/// quality. The dashboard uses the tag to render the right control row
/// (CRF slider vs. bitrate fields) and to explain the rate-control choice
/// in the per-job decisions panel.
/// </summary>
public abstract record RateControl
{
    /// <summary>
    /// Constant-rate factor (quality-driven). Lower = better quality.
    /// </summary>
    public sealed record Crf(int Value) : RateControl;

    /// <summary>
    /// Average-bitrate with max-bitrate ceiling and VBV buffer.
    /// Used when <c>Crf</c> is zero / not set and a target bitrate is given.
    /// </summary>
    public sealed record Abr(int BitrateKbps, int MaxKbps, int BufKbps) : RateControl;

    /// <summary>
    /// CRF with a bitrate ceiling — quality-driven but bounded.
    /// Emitted when both <c>CrfValue</c> and <c>BitrateKbps</c> are non-zero.
    /// </summary>
    public sealed record CrfCapped(int CrfValue, int MaxKbps, int BufKbps) : RateControl;
}
