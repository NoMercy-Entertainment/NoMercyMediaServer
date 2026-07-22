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
using Newtonsoft.Json;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Api.Controllers.V1.Encoder;

/// <summary>
/// Admin-only endpoints for managing Ed25519 trusted publisher keys used to
/// verify signatures on imported encoding profiles. All operations require the
/// requesting user to be the server owner.
/// </summary>
[ApiController]
[Tags(tags: "Encoder Trusted Publishers")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Owner")]
[Route(template: "api/v{version:apiVersion}/encoder/trusted-publishers")]
public class EncoderTrustedPublishersController(MediaContext mediaContext) : BaseController
{
    /// <summary>
    /// Returns all registered trusted publisher keys.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {

        IReadOnlyList<TrustedPublisherKey> keys = await mediaContext
            .TrustedPublisherKeys.AsNoTracking()
            .OrderBy(keySelector: k => k.AddedAt)
            .ToListAsync();

        return Ok(value: new { data = keys });
    }

    /// <summary>
    /// Registers a new trusted Ed25519 public key.
    /// The fingerprint (SHA-256 hex of the raw key bytes) is computed server-side
    /// so the caller only needs to supply the base64-encoded public key and a label.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AddTrustedPublisherRequest request)
    {

        // --- Validate base64 decodes to exactly 32 bytes (Ed25519 key length) ---
        byte[] publicKeyBytes;
        try
        {
            publicKeyBytes = Convert.FromBase64String(s: request.PublicKeyBase64);
        }
        catch (FormatException)
        {
            ValidationEnvelope decodeError = ValidationEnvelope.FromRules(rules:
            [
                new(
                    Id: EncoderRuleId.TrustedPublisherPublicKeyInvalid,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "public_key_base64",
                    Message: "Public key must be a 32-byte Ed25519 key, base64-encoded.",
                    Fix: "Re-export the publisher's key with `openssl pkey -in key.pem -pubout -outform DER | tail -c 32 | base64`."
                ),
            ]);
            return UnprocessableEntity(error: decodeError);
        }

        if (publicKeyBytes.Length != 32)
        {
            ValidationEnvelope lengthError = ValidationEnvelope.FromRules(rules:
            [
                new(
                    Id: EncoderRuleId.TrustedPublisherPublicKeyInvalid,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "public_key_base64",
                    Message: "Public key must be a 32-byte Ed25519 key, base64-encoded.",
                    Fix: "Re-export the publisher's key with `openssl pkey -in key.pem -pubout -outform DER | tail -c 32 | base64`."
                ),
            ]);
            return UnprocessableEntity(error: lengthError);
        }

        // --- Compute fingerprint ---
        string fingerprint = PublicKeyFingerprint.Compute(publicKeyBytes: publicKeyBytes);

        // --- Conflict check ---
        bool exists = await mediaContext
            .TrustedPublisherKeys.AsNoTracking()
            .AnyAsync(predicate: k => k.Fingerprint == fingerprint);

        if (exists)
        {
            ValidationEnvelope conflictError = ValidationEnvelope.FromRules(rules:
            [
                new(
                    Id: EncoderRuleId.TrustedPublisherAlreadyTrusted,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "public_key_base64",
                    Message: $"A trusted key with fingerprint '{fingerprint}' is already registered.",
                    Fix: "This public key is already trusted. No action needed."
                ),
            ]);
            return Conflict(error: conflictError);
        }

        // --- Persist ---
        TrustedPublisherKey row = new()
        {
            Fingerprint = fingerprint,
            Label = request.Label,
            PublicKeyBase64 = request.PublicKeyBase64,
            AddedAt = DateTime.UtcNow,
            AddedBy = User.UserId().ToString(),
        };

        mediaContext.TrustedPublisherKeys.Add(entity: row);
        await mediaContext.SaveChangesAsync();

        return CreatedAtAction(actionName: nameof(Create), routeValues: new { fingerprint = row.Fingerprint }, value: row);
    }

    /// <summary>
    /// Removes a trusted publisher key by its fingerprint. Returns 404 when
    /// the fingerprint is not registered.
    /// </summary>
    [HttpDelete(template: "{fingerprint}")]
    public async Task<IActionResult> Delete(string fingerprint)
    {

        TrustedPublisherKey? existing = await mediaContext.TrustedPublisherKeys.FirstOrDefaultAsync(
            predicate: k => k.Fingerprint == fingerprint
        );

        if (existing is null)
            return NotFoundResponse(detail: $"No trusted key with fingerprint '{fingerprint}' found");

        mediaContext.TrustedPublisherKeys.Remove(entity: existing);
        await mediaContext.SaveChangesAsync();

        return NoContent();
    }
}

public record AddTrustedPublisherRequest(
    [property: JsonProperty(propertyName: "label")] string Label,
    [property: JsonProperty(propertyName: "public_key_base64")] string PublicKeyBase64
);
