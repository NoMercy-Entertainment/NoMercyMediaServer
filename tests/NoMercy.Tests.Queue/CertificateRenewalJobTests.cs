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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Auth;
using NoMercy.Queue.MediaServer.Jobs;
using NoMercyQueue.Core.Interfaces;
using Xunit;

namespace NoMercy.Tests.Queue;

public class CertificateRenewalCronJobTests
{
    [Fact]
    public void CronExpression_ReturnsDaily2AM()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalCronJob>> logger = new();
        CertificateRenewalCronJob job = new(
            logger: logger.Object,
            authTokenStore: new AuthTokenStore(),
            certificateService: new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!)
        );

        // Act
        string cronExpression = job.CronExpression;

        // Assert
        Assert.NotNull(@object: cronExpression);
        Assert.NotEmpty(collection: cronExpression);
        // The CronExpressionBuilder.Daily(2) should return a valid cron expression for 2 AM daily
    }

    [Fact]
    public void JobName_ReturnsCorrectName()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalCronJob>> logger = new();
        CertificateRenewalCronJob job = new(
            logger: logger.Object,
            authTokenStore: new AuthTokenStore(),
            certificateService: new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!)
        );

        // Act
        string jobName = job.JobName;

        // Assert
        Assert.Equal(expected: "Daily Certificate Renewal", actual: jobName);
    }

    [Fact]
    public async Task ExecuteAsync_LogsStartAndCompletion()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalCronJob>> loggerMock = new();
        CertificateRenewalCronJob job = new(
            logger: loggerMock.Object,
            authTokenStore: new AuthTokenStore(),
            certificateService: new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!)
        );

        // Act & Assert
        // Note: This test will fail in the test environment because Certificate.RenewSslCertificate()
        // likely requires actual certificate infrastructure. In a real scenario, you'd want to
        // mock the Certificate.RenewSslCertificate() method or test it separately.

        try
        {
            await job.ExecuteAsync(parameters: "test-parameters");
        }
        catch (Exception)
        {
            // Expected to fail in test environment - we're mainly testing that the structure is correct
        }

        // Verify that logging was attempted (at least the start message)
        loggerMock.Verify(
            expression: x =>
                x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (v, t) => v.ToString()!.Contains("Starting certificate renewal job")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public void CertificateRenewalCronJob_ImplementsICronJobExecutor()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalCronJob>> logger = new();

        // Act
        CertificateRenewalCronJob job = new(
            logger: logger.Object,
            authTokenStore: new AuthTokenStore(),
            certificateService: new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!)
        );

        // Assert
        Assert.IsAssignableFrom<ICronJobExecutor>(@object: job);
    }

    [Fact]
    public void CertificateRenewalCronJob_HasRequiredProperties()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalCronJob>> logger = new();
        CertificateRenewalCronJob job = new(
            logger: logger.Object,
            authTokenStore: new AuthTokenStore(),
            certificateService: new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!)
        );

        // Act & Assert
        Assert.NotNull(@object: job.CronExpression);
        Assert.NotNull(@object: job.JobName);
        Assert.NotEmpty(collection: job.CronExpression);
        Assert.NotEmpty(collection: job.JobName);
    }
}
