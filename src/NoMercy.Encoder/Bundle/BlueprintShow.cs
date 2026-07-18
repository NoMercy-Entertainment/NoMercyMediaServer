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

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// The parent show of an episode identity — present only when
/// <see cref="BlueprintIdentity.Type"/> is <c>"episode"</c>.
/// </summary>
public record BlueprintShow(
    [property: JsonProperty("tmdb_id")] long TmdbId,
    [property: JsonProperty("title")] string Title
);
