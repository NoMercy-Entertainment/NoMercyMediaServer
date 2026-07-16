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

namespace NoMercy.Encoder.BuildingBlocks;

/// <summary>
/// One resolved <c>-dump_attachment</c> target: the source attachment's
/// stream index and the sanitized, collision-free relative path (e.g.
/// "fonts/Arial.ttf") ffmpeg should write it to.
/// </summary>
public record AttachmentDumpTarget(int Index, string RelativePath);
