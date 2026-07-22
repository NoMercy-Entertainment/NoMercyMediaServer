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

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Integration")]
public class SignalRDetailedErrorsTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public SignalRDetailedErrorsTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ProductionSignalR_DoesNotEnableDetailedErrors()
    {
        // The test factory runs without --dev flag, so Config.IsDev is false (production mode).
        // Verify that EnableDetailedErrors is disabled in production.
        IOptions<HubOptions> hubOptions = _factory.Services.GetRequiredService<
            IOptions<HubOptions>
        >();

        Assert.False(
            condition: hubOptions.Value.EnableDetailedErrors,
            userMessage: "SignalR EnableDetailedErrors must be false in production to prevent stack trace leakage to clients"
        );
    }

    [Fact]
    public void SignalR_MaximumReceiveMessageSize_IsReasonablyLimited()
    {
        IOptions<HubOptions> hubOptions = _factory.Services.GetRequiredService<
            IOptions<HubOptions>
        >();
        long? maxSize = hubOptions.Value.MaximumReceiveMessageSize;

        Assert.NotNull(value: maxSize);

        long tenMb = 10L * 1024 * 1024;
        Assert.True(
            condition: maxSize <= tenMb,
            userMessage: $"SignalR MaximumReceiveMessageSize should be at most 10MB but was {maxSize / (1024 * 1024.0):F1}MB"
        );

        Assert.True(
            condition: maxSize >= 1024 * 1024,
            userMessage: $"SignalR MaximumReceiveMessageSize should be at least 1MB but was {maxSize / 1024.0:F0}KB"
        );
    }
}
