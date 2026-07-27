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

using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Security;
using NoMercy.Database.Models.Security;

namespace NoMercy.Api.Controllers;

/// <summary>
/// The deny-list a firewall pulls. pfBlockerNG, like every other feed consumer,
/// takes a URL and nothing else — no header, no credential — so the token lives
/// in the path.
/// </summary>
/// <remarks>
/// A wrong or missing token answers 404 rather than 401 so the endpoint does not
/// confirm its own existence to anyone scanning for it.
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("security/blocklist")]
public class BlocklistController(IAbuseGuard abuseGuard, IBlocklistFeedSettings feedSettings)
    : ControllerBase
{
    [HttpGet]
    [Route("{token}")]
    public async Task<IActionResult> Feed(string token, CancellationToken ct)
    {
        if (!await feedSettings.VerifyAsync(token, ct))
            return NotFound();

        List<IpBan> bans = await abuseGuard.ActiveBansAsync(ct);

        StringBuilder builder = new();
        foreach (IpBan ban in bans)
            builder.Append(ban.Address).Append('\n');

        return Content(builder.ToString(), "text/plain", Encoding.UTF8);
    }
}
