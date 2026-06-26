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

/*
 * This file is part of the NoMercy Entertainment application.
 * Copyright (c) NoMercy Entertainment. All rights reserved.
 * Licensed under the MIT License.
 */

using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Inbox;

namespace NoMercy.Data.Repositories;

public interface IInboxRepository
{
    Task<List<InboxItem>> GetAllAsync(string? status, CancellationToken ct = default);

    Task<InboxItem?> GetByIdAsync(Ulid id, CancellationToken ct = default);

    Task<InboxItem?> GetTrackedByIdAsync(Ulid id, CancellationToken ct = default);

    Task<Folder?> GetFolderByIdAsync(Ulid folderId, CancellationToken ct = default);

    Task ExecuteAssignmentAsync(
        InboxItem item,
        CandidateMatch match,
        InboxDestination destination,
        CancellationToken ct = default
    );

    Task DismissAsync(InboxItem item, CancellationToken ct = default);

    Task DeleteAsync(InboxItem item, CancellationToken ct = default);
}
