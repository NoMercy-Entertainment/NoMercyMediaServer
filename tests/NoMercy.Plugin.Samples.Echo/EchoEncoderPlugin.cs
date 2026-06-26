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

using Microsoft.Extensions.Logging;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugin.Samples.Echo;

public class EchoEncoderPlugin : IEncoderPlugin
{
    public string Name => "Echo";
    public string Description => "Sample plugin — returns a fixed encoding profile for any input.";
    public Guid Id { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public Version Version { get; } = new(0, 1, 0);

    public void Initialize(IPluginContext context)
    {
        context.Logger.LogInformation("Echo plugin initialized");
    }

    public EncodingProfile GetProfile(MediaInfo info) =>
        new()
        {
            Name = $"Echo-{Path.GetFileName(info.FilePath)}",
            VideoCodec = "h264",
            AudioCodec = "aac",
        };

    public void Dispose() { }
}
