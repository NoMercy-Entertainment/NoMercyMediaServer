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

namespace NoMercy.Encoder.Startup;

/// <summary>
/// Lets the deferred hardware benchmark ask the host whether the encoder is
/// currently doing user-visible work (live transcode session, queued encode
/// job). The Encoder project can't see the queue or streaming session
/// manager directly, so a concrete probe lives in the hosting project and
/// is injected here. Default implementation returns <c>false</c> so test
/// harnesses and standalone encoder uses behave unchanged.
/// </summary>
public interface IEncoderActivityProbe
{
    bool IsBusy { get; }
}

internal sealed class NullEncoderActivityProbe : IEncoderActivityProbe
{
    public bool IsBusy => false;
}
