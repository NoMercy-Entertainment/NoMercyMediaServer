using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;

namespace NoMercy.Tests.Database;

public class TokenStoreTests : IDisposable
{
    public TokenStoreTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(provider);
    }

    public void Dispose() { }

    [Fact]
    public void EncryptDecrypt_Roundtrip_ReturnsOriginal()
    {
        string original = "my-secret-token-value";
        string encrypted = TokenStore.EncryptToken(original);
        string? decrypted = TokenStore.DecryptToken(encrypted);

        Assert.NotEqual(original, encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void EncryptToken_NullInput_ReturnsEmpty()
    {
        string result = TokenStore.EncryptToken(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EncryptToken_EmptyInput_ReturnsEmpty()
    {
        string result = TokenStore.EncryptToken("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void DecryptToken_NullInput_ReturnsNull()
    {
        string? result = TokenStore.DecryptToken(null);
        Assert.Null(result);
    }

    [Fact]
    public void DecryptToken_EmptyInput_ReturnsNull()
    {
        string? result = TokenStore.DecryptToken("");
        Assert.Null(result);
    }

    [Fact]
    public void DecryptToken_GarbageInput_ReturnsNull()
    {
        string? result = TokenStore.DecryptToken("not-a-valid-encrypted-string");
        Assert.Null(result);
    }

    [Fact]
    public void DecryptToken_GarbageInput_DoesNotReturnGarbage()
    {
        string garbage = "not-a-valid-encrypted-string";
        string? result = TokenStore.DecryptToken(garbage);
        Assert.NotEqual(garbage, result);
    }
}
