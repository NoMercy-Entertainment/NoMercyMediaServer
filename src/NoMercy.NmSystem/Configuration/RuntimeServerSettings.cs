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

using NoMercyQueue.Core;

namespace NoMercy.NmSystem.Configuration;

// Process-wide mutable runtime server settings: hydrated from the database
// Configuration table at boot (UserSettings) and mutable at runtime (dashboard).
// Consumers should inject this singleton via DI; the static Current accessor
// bridges the legacy static boot path (and the Config facade) that cannot yet
// resolve services. DI hands out this same Current instance.
public class RuntimeServerSettings
{
    public static RuntimeServerSettings Current { get; } = new();

    public int InternalServerPort { get; set; } = 7626;

    public int ExternalServerPort { get; set; } = 7626;

    public int StunPort => InternalServerPort + 1;

    public KeyValuePair<string, int> LibraryWorkers { get; set; } = new(key: QueueNames.Library, value: 1);

    public KeyValuePair<string, int> ImportWorkers { get; set; } = new(key: QueueNames.Import, value: 2);

    public KeyValuePair<string, int> ExtrasWorkers { get; set; } = new(key: QueueNames.Extras, value: 4);

    public KeyValuePair<string, int> EncoderWorkers { get; set; } = new(key: QueueNames.Encoder, value: 1);

    public KeyValuePair<string, int> EncoderTaskWorkers { get; set; } =
        new(key: QueueNames.EncoderTask, value: 0);

    public KeyValuePair<string, int> GpuEncoderWorkers { get; set; } =
        new(key: QueueNames.EncoderGpu, value: 1);

    public KeyValuePair<string, int> CpuEncoderWorkers { get; set; } =
        new(key: QueueNames.EncoderCpu, value: 1);

    public KeyValuePair<string, int> CronWorkers { get; set; } = new(key: QueueNames.Cron, value: 1);

    public KeyValuePair<string, int> ImageWorkers { get; set; } = new(key: QueueNames.Image, value: 3);

    public KeyValuePair<string, int> FileWorkers { get; set; } = new(key: QueueNames.File, value: 2);

    public KeyValuePair<string, int> MusicWorkers { get; set; } = new(key: QueueNames.Music, value: 2);

    public KeyValuePair<string, int> PaletteWorkers { get; set; } = new(key: QueueNames.Palette, value: 1);

    public bool Swagger { get; set; } = true;

    public bool UseSynthesizedDns { get; set; }

    public bool? AllowAdultContent { get; set; }

    // Safe-by-default: adult content is shown only when explicitly enabled.
    // A null (never configured) or false setting both resolve to hidden.
    public bool ShowAdultContent => AllowAdultContent == true;
}
