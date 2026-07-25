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
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Dashboard;

public record UserPermissionRequest
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("manage")]
    public bool Manage { get; set; }

    [JsonProperty("owner")]
    public bool Owner { get; set; }

    [JsonProperty("allowed")]
    public bool Allowed { get; set; }

    [JsonProperty("audio_transcoding")]
    public bool AudioTranscoding { get; set; }

    [JsonProperty("video_transcoding")]
    public bool VideoTranscoding { get; set; }

    [JsonProperty("no_transcoding")]
    public bool NoTranscoding { get; set; }

    [JsonProperty("libraries")]
    public Ulid[] Libraries { get; set; } = [];

    public UserPermissionRequest()
    {
        //
    }

    public UserPermissionRequest(User user)
    {
        Id = user.Id;
        Manage = user.Manage;
        Owner = user.Owner;
        Allowed = user.Allowed;
        AudioTranscoding = user.AudioTranscoding;
        VideoTranscoding = user.VideoTranscoding;
        NoTranscoding = user.NoTranscoding;

        Libraries = user.LibraryUser.Select(libraryUser => libraryUser.LibraryId).ToArray();
    }
}
