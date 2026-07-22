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

using Microsoft.Extensions.Logging;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Auth;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Queue.MediaServer.Jobs;

public class CertificateRenewalCronJob : ICronJobExecutor
{
    private readonly ILogger<CertificateRenewalCronJob> _logger;

    public string CronExpression => new CronExpressionBuilder().Daily(hour: 2);
    public string JobName => "Daily Certificate Renewal";

    private readonly IAuthTokenStore _authTokenStore;
    private readonly ICertificateService _certificateService;

    public CertificateRenewalCronJob(
        ILogger<CertificateRenewalCronJob> logger,
        IAuthTokenStore authTokenStore,
        ICertificateService certificateService
    )
    {
        _authTokenStore = authTokenStore;
        _certificateService = certificateService;
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(message: "Starting certificate renewal job");

        await _certificateService.RenewSslCertificate(accessToken: _authTokenStore.AccessToken);

        _logger.LogInformation(message: "Certificate renewal job completed");
    }
}
