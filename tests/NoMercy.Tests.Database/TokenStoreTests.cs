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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.NmSystem.Security;

namespace NoMercy.Tests.Database;

public class TokenStoreTests : IDisposable
{
    public TokenStoreTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(serviceProvider: provider);
    }

    public void Dispose() { }

    [Fact]
    public void EncryptDecrypt_Roundtrip_ReturnsOriginal()
    {
        string original = "my-secret-token-value";
        string encrypted = TokenStore.EncryptToken(token: original);
        string? decrypted = TokenStore.DecryptToken(token: encrypted);

        Assert.NotEqual(expected: original, actual: encrypted);
        Assert.Equal(expected: original, actual: decrypted);
    }

    [Fact]
    public void EncryptToken_NullInput_ReturnsEmpty()
    {
        string result = TokenStore.EncryptToken(token: null);
        Assert.Equal(expected: string.Empty, actual: result);
    }

    [Fact]
    public void EncryptToken_EmptyInput_ReturnsEmpty()
    {
        string result = TokenStore.EncryptToken(token: "");
        Assert.Equal(expected: string.Empty, actual: result);
    }

    [Fact]
    public void DecryptToken_NullInput_ReturnsNull()
    {
        string? result = TokenStore.DecryptToken(token: null);
        Assert.Null(@object: result);
    }

    [Fact]
    public void DecryptToken_EmptyInput_ReturnsNull()
    {
        string? result = TokenStore.DecryptToken(token: "");
        Assert.Null(@object: result);
    }

    [Fact]
    public void DecryptToken_GarbageInput_ReturnsNull()
    {
        string? result = TokenStore.DecryptToken(token: "not-a-valid-encrypted-string");
        Assert.Null(@object: result);
    }

    [Fact]
    public void DecryptToken_GarbageInput_DoesNotReturnGarbage()
    {
        string garbage = "not-a-valid-encrypted-string";
        string? result = TokenStore.DecryptToken(token: garbage);
        Assert.NotEqual(expected: garbage, actual: result);
    }
}
