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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Intake;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.MediaProcessing.Intake;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Api.Controllers.V1.Intake;

/// <summary>
/// Authenticated inbound intake webhook. A render client (or any authorized
/// producer) POSTs the path of a file it dropped into the configured drop
/// folder; the server re-triggers the existing Inbox pipeline
/// (<see cref="NoMercy.MediaProcessing.EventHandlers.InboxClassifierEventHandler"/>)
/// to pick it up.
///
/// <see cref="AllowAnonymousAttribute"/> is deliberate — this is a
/// machine-to-machine endpoint, not a Keycloak user session. The
/// X-Intake-Token header is the ONLY gate: it is verified with a
/// constant-time comparison inside <see cref="IIntakeSettings"/> and checked
/// before any other work happens. Any failure — missing header, wrong token,
/// or an exception from the verifier — fails closed to 401. The token value
/// itself is never logged.
/// </summary>
[ApiController]
[Tags(tags: "Intake")]
[ApiVersion(version: 1.0)]
[AllowAnonymous]
[Route(template: "api/v{version:apiVersion}/intake/webhook")]
public class IntakeWebhookController(
    IIntakeSettings intakeSettings,
    IDbContextFactory<MediaContext> contextFactory
) : BaseController
{
    private const string TokenHeaderName = "X-Intake-Token";

    [HttpPost]
    public async Task<IActionResult> Webhook(
        [FromBody] IntakeWebhookRequest? request,
        CancellationToken ct
    )
    {
        string presentedToken = Request.Headers[key: TokenHeaderName].ToString();

        bool tokenIsValid;
        try
        {
            tokenIsValid =
                !string.IsNullOrEmpty(value: presentedToken)
                && await intakeSettings.VerifyTokenAsync(presented: presentedToken, ct: ct);
        }
        catch
        {
            tokenIsValid = false;
        }

        if (!tokenIsValid)
            return UnauthenticatedResponse(detail: "Missing or invalid intake token.");

        string? dropFolder = await intakeSettings.GetDropFolderAsync(ct: ct);
        if (string.IsNullOrEmpty(value: dropFolder))
            return ConflictResponse(detail: "No drop folder is configured for intake.");

        if (
            request is null
            || string.IsNullOrWhiteSpace(value: request.Path)
            || !IsPathUnderRoot(candidatePath: request.Path, rootPath: dropFolder)
        )
            return BadRequestResponse(
                detail: "path is required and must resolve to a location inside the configured drop folder."
            );

        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<Library> inboxLibraries = await context
            .Libraries.AsNoTracking()
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .Where(predicate: library => library.Type == MediaTypes.InboxMediaType)
            .ToListAsync(cancellationToken: ct);

        FolderLibrary? ownedFolder = inboxLibraries
            .SelectMany(selector: library => library.FolderLibraries)
            .FirstOrDefault(predicate: folderLibrary =>
                FolderOwnsDropFolder(folderPath: folderLibrary.Folder.Path, dropFolder: dropFolder)
            );

        if (ownedFolder is null)
            return ConflictResponse(
                detail: "The configured drop folder is not registered as an inbox library."
            );

        if (!EventBusProvider.IsConfigured)
            return ServiceUnavailableResponse(
                detail: "The event bus is not configured; the dropped file cannot be processed right now."
            );

        await EventBusProvider.Current.PublishAsync(
            @event: new FileCreatedEvent
            {
                FolderPath = ownedFolder.Folder.Path,
                LibraryId = ownedFolder.LibraryId,
                LibraryType = MediaTypes.InboxMediaType,
            },
            ct: ct
        );

        return StatusCode(statusCode: StatusCodes.Status202Accepted, value: new { status = "accepted" });
    }

    private static bool IsPathUnderRoot(string candidatePath, string rootPath)
    {
        string fullRoot = Path.GetFullPath(path: rootPath)
            .TrimEnd(trimChars: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        string fullCandidate = Path.GetFullPath(path: candidatePath);
        string rootWithSeparator = fullRoot + Path.DirectorySeparatorChar;

        return fullCandidate.Equals(value: fullRoot, comparisonType: StringComparison.OrdinalIgnoreCase)
            || fullCandidate.StartsWith(value: rootWithSeparator, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static bool FolderOwnsDropFolder(string folderPath, string dropFolder)
    {
        string normalizedFolder = NormalizeForComparison(path: folderPath);
        string normalizedDrop = NormalizeForComparison(path: dropFolder);

        return normalizedDrop.Equals(value: normalizedFolder, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForComparison(string path) =>
        path.Replace(oldChar: '\\', newChar: '/').TrimEnd(trimChar: '/');
}
