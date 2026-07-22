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
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Intake;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

/// <summary>
/// Authenticated dashboard surface for configuring intake (drop folder +
/// webhook token). Separate from <c>IntakeWebhookController</c>, which is the
/// anonymous, token-gated machine-to-machine endpoint a render client calls to
/// notify the server of a dropped file.
/// </summary>
[ApiController]
[Tags(tags: "Dashboard Intake")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/intake", Order = 10)]
public class IntakeController(
    IIntakeSettings intakeSettings,
    MediaContext mediaContext,
    ILogger<IntakeController> logger
) : BaseController
{
    private const string WebhookPath = "api/v1/intake/webhook";
    private const string WebhookHeader = "X-Intake-Token";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        string? dropFolder = await intakeSettings.GetDropFolderAsync(ct: ct);
        bool hasToken = await intakeSettings.HasTokenAsync(ct: ct);

        return Ok(
            value: new
            {
                dropFolder,
                hasToken,
                webhookPath = WebhookPath,
                webhookHeader = WebhookHeader,
            }
        );
    }

    [HttpPut]
    [Route(template: "drop-folder")]
    public async Task<IActionResult> SetDropFolder(
        [FromBody] SetDropFolderRequest? request,
        CancellationToken ct
    )
    {
        string? path = request?.Path;

        if (string.IsNullOrWhiteSpace(value: path))
        {
            await intakeSettings.SetDropFolderAsync(path: null, ct: ct);
            return Ok(value: new { dropFolder = (string?)null });
        }

        List<Library> inboxLibraries = await mediaContext
            .Libraries.AsNoTracking()
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .Where(predicate: library => library.Type == MediaTypes.InboxMediaType)
            .ToListAsync(cancellationToken: ct);

        bool isInboxLibraryFolder = inboxLibraries
            .SelectMany(selector: library => library.FolderLibraries)
            .Any(predicate: folderLibrary => PathsMatch(folderPath: folderLibrary.Folder.Path, candidatePath: path));

        if (!isInboxLibraryFolder)
            return BadRequestResponse(
                detail: "The drop folder must be a folder of an Inbox-type library. Create/point an Inbox library at this folder first."
            );

        await intakeSettings.SetDropFolderAsync(path: path, ct: ct);

        try
        {
            // Best-effort: a watcher-refresh failure must not fail the settings
            // save — the webhook intake path works regardless of live watch status.
            LibraryFileWatcher.RefreshLibraryCache();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception: exception,
                message: "Failed to refresh the library watcher cache after updating the intake drop folder to {DropFolder}",
                args: path
            );
        }

        return Ok(value: new { dropFolder = path });
    }

    [HttpPost]
    [Route(template: "token")]
    public async Task<IActionResult> IssueToken(CancellationToken ct)
    {
        string token = await intakeSettings.IssueTokenAsync(ct: ct);

        return Ok(value: new { token });
    }

    private static bool PathsMatch(string folderPath, string candidatePath) =>
        NormalizeForComparison(path: folderPath)
            .Equals(value: NormalizeForComparison(path: candidatePath), comparisonType: StringComparison.OrdinalIgnoreCase);

    private static string NormalizeForComparison(string path) =>
        path.Replace(oldChar: '\\', newChar: '/').TrimEnd(trimChar: '/');
}
