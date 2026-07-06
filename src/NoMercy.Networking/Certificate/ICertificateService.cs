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
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace NoMercy.Networking.Certificate;

public interface ICertificateService
{
    void LoadFromDb();
    bool HasValidCertificate();

    /// <summary>
    /// True when a real Let's Encrypt cert exists, OR the self-signed fallback is
    /// available/generated. Broader than <see cref="HasValidCertificate"/> — use that one
    /// where "real cert" specifically means "registered" (BootOrchestrator, PortManager).
    /// </summary>
    bool EnsureHttpsCertificate();
    void KestrelConfig(KestrelServerOptions options);
    void ConfigureHttpsListener(ListenOptions listenOptions);
    Task RenewSslCertificate(string? accessToken, int maxRetries = 30);
}
