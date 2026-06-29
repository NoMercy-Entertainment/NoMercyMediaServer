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

namespace NoMercy.NmSystem.Lifecycle;

/// <summary>
/// Coarse-grained boot stages a subsystem can mark complete. Stages run in
/// parallel — there is no implicit ordering — so consumers wait for the
/// specific stage(s) they depend on. Use <see cref="All"/> for "fully idle".
/// </summary>
[Flags]
public enum BootStage
{
    None = 0,

    /// <summary>Phase 1 essentials: app folders, settings, ApiInfo.</summary>
    Essential = 1 << 0,

    /// <summary>Tokens loaded + validated (or interactive auth pending in setup mode).</summary>
    Auth = 1 << 1,

    /// <summary>Every external binary (ffmpeg, yt-dlp, whisper models, tesseract) on disk
    /// and not in flux — safe for the encoder worker to spawn ffmpeg.</summary>
    Binaries = 1 << 2,

    /// <summary>External IP discovered, UPnP / port-forward attempted, connectivity confirmed.</summary>
    Network = 1 << 3,

    /// <summary>Hardware probe complete: GPU enumerated, encoder speed index seeded.</summary>
    Hardware = 1 << 4,

    /// <summary>SSL certificate acquired and server registered with the cloud API.</summary>
    Registered = 1 << 5,

    /// <summary>Composite "server ready" stage — login, registration, SSL and the
    /// rest of the user-facing boot have completed, so queue workers may run.
    /// Deliberately EXCLUDES <see cref="Hardware"/>: GPU/encoder detection is a
    /// deferred background task that must not hold up the server becoming usable.
    /// The encoder queues additionally wait on <see cref="Hardware"/> through their
    /// per-queue ready stage so encodes don't start before detection completes.</summary>
    All = Essential | Auth | Binaries | Network | Registered,
}
