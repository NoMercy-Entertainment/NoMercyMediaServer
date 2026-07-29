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

using System.Text.Json.Serialization;

namespace NoMercy.Notifications.Push;

public record PushPayload(
    [property: JsonPropertyName("Title")] string Title,
    [property: JsonPropertyName("Body")] string Body,
    [property: JsonPropertyName("Route")] string? Route,
    [property: JsonPropertyName("Category")] string? Category = null,
    IReadOnlyList<PushAction>? Actions = null,
    [property: JsonPropertyName("Image")] string? Image = null,
    [property: JsonPropertyName("Icon")] string? Icon = null
)
{
    [JsonPropertyName("Actions")]
    public IReadOnlyList<PushAction> Actions { get; init; } = Actions ?? [];
}
