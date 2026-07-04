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
using NoMercy.Database.Models.Media;

namespace NoMercy.Data.DTOs.Encoder;

public class EncoderProfileFolderDto
{
    [JsonProperty("encoder_profile_id")]
    public Ulid EncoderProfileId { get; set; }

    [JsonProperty("folder_id")]
    public Ulid FolderId { get; set; }

    [JsonProperty("folder")]
    public FolderLibraryDto FolderLibrary { get; set; } = new();

    public EncoderProfileFolderDto() { }

    public EncoderProfileFolderDto(EncoderProfileFolder encoderProfileFolder)
    {
        EncoderProfileId = encoderProfileFolder.EncoderProfileId;
        FolderId = encoderProfileFolder.FolderId;
        FolderLibrary = new(encoderProfileFolder.Folder.FolderLibraries);
    }
}
