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

namespace NoMercy.MediaProcessing.Inbox;

public sealed class InboxDestination
{
    public Ulid LibraryId { get; set; }
    public Ulid FolderId { get; set; }
    public Ulid ProfileId { get; set; }
    public Ulid DriverId { get; set; }
    public string FolderPath { get; set; } = string.Empty;
}
