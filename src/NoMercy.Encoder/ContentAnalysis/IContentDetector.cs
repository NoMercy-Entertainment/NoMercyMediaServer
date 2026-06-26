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

namespace NoMercy.Encoder.ContentAnalysis;

public interface IContentDetector
{
    Task<ContentSegment[]> DetectAsync(string inputPath, CancellationToken ct);

    Task<ContentSegment[]> DetectIntroOutroAsync(string[] episodePaths, CancellationToken ct);
}

public record ContentSegment(
    TimeSpan Start,
    TimeSpan End,
    ContentSegmentType Type,
    double Confidence
);

public enum ContentSegmentType
{
    Intro,
    Outro,
    Commercial,
    Recap,
    Content,
}
