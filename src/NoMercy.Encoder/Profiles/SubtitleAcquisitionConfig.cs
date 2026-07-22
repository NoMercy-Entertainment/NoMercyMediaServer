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

namespace NoMercy.Encoder.Profiles;

public enum SubtitleProvider
{
    OpenSubtitles,
}

public enum SubtitleMatchStrategy
{
    HashOnly,
    HashThenFilename,
    HashThenFilenameThenTitle,
    TitleOnly,
}

public enum SubtitleEmbedPolicy
{
    ExactMatchOnly,
    AlwaysSidecar,
}

public record SubtitleAcquisitionConfig
{
    public bool Enabled { get; init; }
    public SubtitleProvider[] Providers { get; init; } = [SubtitleProvider.OpenSubtitles];
    public string[] Languages { get; init; } = [];
    public SubtitleMatchStrategy Strategy { get; init; } =
        SubtitleMatchStrategy.HashThenFilenameThenTitle;
    public int MaxPerLanguage { get; init; } = 1;
    public double MinRating { get; init; }
    public int MinDownloads { get; init; }
    public bool TrustedUploadersOnly { get; init; }
    public bool RequireMatchingFps { get; init; }
    public TimeSpan PerRequestTimeout { get; init; } = TimeSpan.FromSeconds(seconds: 5);
    public bool FillMissingOnly { get; init; } = true;
    public SubtitleEmbedPolicy EmbedPolicy { get; init; } = SubtitleEmbedPolicy.ExactMatchOnly;
}
