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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Subtitles;

public record AcquisitionRequest(
    string SourcePath,
    long SourceFileSize,
    string SourceFilename,
    string MediaTitle,
    int? Season,
    int? Episode,
    int? Year,
    double? SourceFps,
    TimeSpan SourceDuration,
    string[] LanguagesAlreadyInSource,
    SubtitleAcquisitionConfig Config
);
