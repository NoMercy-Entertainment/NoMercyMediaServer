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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// One entry per source stream, built by walking <c>source.ffprobe.streams[]</c>.
/// Names the single most-faithful retained artifact for that stream — see spec
/// "Tracks and artifact selection".
/// </summary>
public record BlueprintTrack(
    /// <summary>Joins into <c>source.ffprobe.streams[index]</c> in this same file.</summary>
    [property: JsonProperty(propertyName: "source_stream_index")] int SourceStreamIndex,
    /// <summary>"video" | "audio" | "subtitle".</summary>
    [property: JsonProperty(propertyName: "kind")] string Kind,
    [property: JsonProperty(propertyName: "source_codec")] string SourceCodec,
    [property: JsonProperty(propertyName: "source_language")] string? SourceLanguage,
    /// <summary>"copy" | "extract" | "transcode" | "burn_in" | "acquire".</summary>
    [property: JsonProperty(propertyName: "policy")] string Policy,
    /// <summary>"lossless" | "lossy" | "irreversible" | "external".</summary>
    [property: JsonProperty(propertyName: "fidelity")] string Fidelity,
    /// <summary>False when the accepted-loss transcode is the only retained payload.</summary>
    [property: JsonProperty(propertyName: "reconstructable")] bool? Reconstructable,
    /// <summary>Original codec/params recorded when the stream was an accepted loss.</summary>
    [property: JsonProperty(propertyName: "original_params")] JObject? OriginalParams,
    [property: JsonProperty(propertyName: "container")] string? Container,
    [property: JsonProperty(propertyName: "files")] IReadOnlyList<string> Files,
    /// <summary>Set only for copied or extracted (lossless) assets — see spec "Verification".</summary>
    [property: JsonProperty(propertyName: "sha256")] string? Sha256
);
