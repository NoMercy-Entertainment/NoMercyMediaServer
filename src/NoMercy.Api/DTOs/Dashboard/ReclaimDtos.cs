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
using NoMercy.MediaProcessing.Reclaim;

namespace NoMercy.Api.DTOs.Dashboard;

public sealed class ReclaimableItemDto
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; }

    [JsonProperty(propertyName: "mediaType")]
    public string MediaType { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string Folder { get; set; }

    [JsonProperty(propertyName: "servedCopy")]
    public string ServedCopy { get; set; }

    [JsonProperty(propertyName: "kind")]
    public string Kind { get; set; }

    [JsonProperty(propertyName: "targetCount")]
    public int TargetCount { get; set; }

    [JsonProperty(propertyName: "reclaimableBytes")]
    public long ReclaimableBytes { get; set; }

    public ReclaimableItemDto(ReclaimableItem item)
    {
        Id = item.Id;
        Title = item.Title;
        MediaType = item.MediaType;
        Folder = item.Folder;
        ServedCopy = item.ServedCopy;
        Kind = item.Kind.ToString();
        TargetCount = item.TargetPaths.Count;
        ReclaimableBytes = item.ReclaimableBytes;
    }
}
