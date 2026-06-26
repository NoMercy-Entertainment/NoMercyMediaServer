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
/// Spec-shape for one subtitle stream in the encoding plan. The dashboard
/// renders subtitle plans in a separate table below the variant cards so
/// users can see which tracks will be extracted, burned-in, or OCR'd
/// before committing to the encode.
/// </summary>
public sealed record SubtitlePlan(
    int SourceIndex,
    string Codec,
    string? Language,
    /// <summary>
    /// One of <c>copy</c>, <c>extract</c>, <c>extract_ocr</c>, or <c>burn_in</c>.
    /// Derived from <see cref="NoMercy.Encoder.Pipeline.StreamAction"/> and
    /// <see cref="NoMercy.Encoder.Profiles.SubtitleMode"/>.
    /// </summary>
    string Action
);
