/*
 * This file is part of the NoMercy Entertainment application.
 * Copyright (c) NoMercy Entertainment. All rights reserved.
 * Licensed under the MIT License.
 */

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Inbox;

namespace NoMercy.Data.Repositories;

public class InboxRepository(MediaContext context, InboxRoutingService routingService)
    : IInboxRepository
{
    public Task<List<InboxItem>> GetAllAsync(string? status, CancellationToken ct = default)
    {
        IQueryable<InboxItem> query = context.InboxItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(item => item.Status == status);

        return query.OrderByDescending(item => item.CreatedAt).ToListAsync(ct);
    }

    public Task<InboxItem?> GetByIdAsync(Ulid id, CancellationToken ct = default)
    {
        return context.InboxItems.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
    }

    public Task<InboxItem?> GetTrackedByIdAsync(Ulid id, CancellationToken ct = default)
    {
        return context.InboxItems.FirstOrDefaultAsync(item => item.Id == id, ct);
    }

    public Task<Folder?> GetFolderByIdAsync(Ulid folderId, CancellationToken ct = default)
    {
        return context.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId, ct);
    }

    public Task ExecuteAssignmentAsync(
        InboxItem item,
        CandidateMatch match,
        InboxDestination destination,
        CancellationToken ct = default
    )
    {
        return routingService.ExecuteAssignment(item, match, destination, context, ct);
    }

    public async Task DismissAsync(InboxItem item, CancellationToken ct = default)
    {
        item.Status = "Dismissed";
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(InboxItem item, CancellationToken ct = default)
    {
        context.InboxItems.Remove(item);
        await context.SaveChangesAsync(ct);
    }
}
