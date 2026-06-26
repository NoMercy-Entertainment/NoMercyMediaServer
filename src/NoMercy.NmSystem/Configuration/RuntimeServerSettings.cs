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

    public KeyValuePair<string, int> LibraryWorkers { get; set; } = new("library", 1);

    public KeyValuePair<string, int> ImportWorkers { get; set; } = new("import", 2);

    public KeyValuePair<string, int> ExtrasWorkers { get; set; } = new("extras", 4);

    public KeyValuePair<string, int> EncoderWorkers { get; set; } = new("encoder", 1);

    public KeyValuePair<string, int> EncoderTaskWorkers { get; set; } = new("encoder-task", 0);

    public KeyValuePair<string, int> GpuEncoderWorkers { get; set; } = new("encoder-gpu", 1);

    public KeyValuePair<string, int> CpuEncoderWorkers { get; set; } = new("encoder-cpu", 1);

    public KeyValuePair<string, int> CronWorkers { get; set; } = new("cron", 1);

    public KeyValuePair<string, int> ImageWorkers { get; set; } = new("image", 3);

    public KeyValuePair<string, int> FileWorkers { get; set; } = new("file", 2);

    public KeyValuePair<string, int> MusicWorkers { get; set; } = new("music", 2);

    public KeyValuePair<string, int> PaletteWorkers { get; set; } = new("palette", 1);

    public bool Swagger { get; set; } = true;

    public bool? AllowAdultContent { get; set; }
}
