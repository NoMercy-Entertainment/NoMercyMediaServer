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

using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Database.Maintenance;

/// <summary>
/// Resolves the server's stable identity Guid — the value sent to
/// api.nomercy.tv as "id" during registration (<see cref="Info.DeviceId"/>).
/// A pure hardware fingerprint (motherboard + drive serials) collides across
/// containerized/VM installs: Docker/KVM/VPS environments frequently expose an
/// empty or provider-wide-shared serial, so two different installs of the same
/// image hash to the SAME Guid and the second one to register gets rejected by
/// the backend as "registered to another account". Once resolved here, the id
/// is persisted in app.db and never recomputed — existing registered servers
/// keep the exact hardware-derived id their DNS subdomain and certificate were
/// issued for; a genuinely new/never-registered install gets a fresh unique id.
///
/// A box that already registered on one of the <see cref="KnownDegenerateDeviceIds"/>
/// values is NOT preserved — that id is shared with every other box that hit the
/// same empty-DMI probe, so "prior registration evidence" proves nothing about
/// uniqueness. It is always migrated to a fresh Guid, even with an ssl_certificate
/// on file. That certificate/DNS entry was never uniquely this box's anyway, so the
/// migration also deletes the stale ssl_certificate/ssl_private_key rows — leaving
/// them would make <c>CertificateService.HasValidCertificate()</c> (and therefore
/// BootOrchestrator's "is this server registered" check) keep reporting true for a
/// certificate that doesn't cover the new id, silently skipping re-registration.
/// Auth tokens are never touched, so the re-registration that follows is
/// non-interactive.
/// </summary>
public static class DeviceIdentityResolver
{
    public const string ConfigKey = "device_identity_id";

    // Presence of a previously-issued SSL certificate is the strongest local
    // proof this install already completed registration + assignment under its
    // current hardware-derived id — CertificateService only writes these rows
    // after a successful backend certificate/renew-certificate call. Keep the
    // key names in sync with NoMercy.Networking.Certificate.CertificateService.
    private const string SslCertificateKey = "ssl_certificate";
    private const string SslPrivateKeyKey = "ssl_private_key";

    /// <param name="hardwareDerivedId">
    /// Overrides the hardware fingerprint that would otherwise come from
    /// <see cref="Info.DeviceId"/>. Production call sites never pass this —
    /// it exists so tests can exercise the degenerate-id migration path
    /// without touching the process-wide <see cref="Info"/> static.
    /// </param>
    public static Guid ResolveAndPersist(AppDbContext db, Guid? hardwareDerivedId = null)
    {
        Configuration? existing = db.Configuration.FirstOrDefault(c => c.Key == ConfigKey);
        if (
            existing is not null
            && Guid.TryParse(existing.Value, out Guid persistedId)
            && !KnownDegenerateDeviceIds.IsDegenerate(persistedId)
        )
            return persistedId;

        Guid hardwareId = hardwareDerivedId ?? Info.DeviceId;
        Guid resolvedId;

        if (KnownDegenerateDeviceIds.IsDegenerate(hardwareId))
        {
            resolvedId = Guid.NewGuid();
            Logger.Setup(
                $"Device id {hardwareId} is a known non-unique value; migrating to unique identity {resolvedId}.",
                LogEventLevel.Warning
            );

            // A cert issued for the old (shared, non-unique) id doesn't cover the new
            // id's DNS subdomain and never uniquely proved this box's registration in
            // the first place. Drop it in the SAME SaveChanges as the identity persist
            // below so we never leave a stale cert paired with a fresh id — that would
            // make HasValidCertificate() keep reporting "registered" and BootOrchestrator
            // would skip re-registration under the new identity. Auth tokens are
            // deliberately left untouched so re-registration is non-interactive.
            InvalidateStaleCertificate(db);
        }
        else
        {
            resolvedId = HasEvidenceOfPriorRegistration(db) ? hardwareId : Guid.NewGuid();
        }

        if (existing is not null)
            existing.Value = resolvedId.ToString();
        else
            db.Configuration.Add(new() { Key = ConfigKey, Value = resolvedId.ToString() });

        db.SaveChanges();

        return resolvedId;
    }

    private static void InvalidateStaleCertificate(AppDbContext db)
    {
        List<Configuration> staleCertRows = db
            .Configuration.Where(c => c.Key == SslCertificateKey || c.Key == SslPrivateKeyKey)
            .ToList();

        if (staleCertRows.Count > 0)
            db.Configuration.RemoveRange(staleCertRows);
    }

    private static bool HasEvidenceOfPriorRegistration(AppDbContext db)
    {
        return db.Configuration.Any(c => c.Key == SslCertificateKey || c.Key == SslPrivateKeyKey);
    }
}
