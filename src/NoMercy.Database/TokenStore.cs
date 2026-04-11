using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace NoMercy.Database;

public static class TokenStore
{
    private static IDataProtector? Protector { get; set; }

    public static void Initialize(IServiceProvider serviceProvider)
    {
        if (Protector == null)
        {
            IDataProtectionProvider dataProtectionProvider =
                serviceProvider.GetRequiredService<IDataProtectionProvider>();
            Protector = dataProtectionProvider.CreateProtector(
                "NoMercyMediaServer.TokenProtection"
            );
        }
    }

    public static string EncryptToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return string.Empty;

        if (Protector == null)
            throw new InvalidOperationException(
                "TokenStore not initialized. Call Initialize() first."
            );

        return Protector.Protect(token);
    }

    public static string? DecryptToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        if (Protector == null)
            throw new InvalidOperationException(
                "TokenStore not initialized. Call Initialize() first."
            );

        try
        {
            return Protector.Unprotect(token);
        }
        catch (Exception)
        {
            // Return null — caller treats this as "no value" → triggers re-auth.
            // Never return raw ciphertext (leaks internal state, burns API rate limits).
            return null;
        }
    }
}
