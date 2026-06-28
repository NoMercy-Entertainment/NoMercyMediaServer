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
namespace NoMercyQueue.Core;

/// <summary>
/// Canonical queue-name constants. These are the single source of truth for the
/// queue identifiers that were previously scattered as magic string literals
/// across worker registration, job classes, and resource-aware gating.
/// </summary>
public static class QueueNames
{
    public const string Library = "library";
    public const string Import = "import";
    public const string Extras = "extras";
    public const string Encoder = "encoder";
    public const string EncoderTask = "encoder-task";
    public const string EncoderGpu = "encoder-gpu";
    public const string EncoderCpu = "encoder-cpu";
    public const string Cron = "cron";
    public const string Image = "image";
    public const string File = "file";
    public const string Music = "music";
    public const string Palette = "palette";
}
