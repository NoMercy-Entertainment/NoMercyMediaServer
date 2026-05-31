using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.NmSystem.Information;

namespace NoMercy.Database;

public static class TokenStore
{
    private const string ApplicationName = "NoMercyMediaServer";
    private const string ProtectorPurpose = "NoMercyMediaServer.TokenProtection";

    private static readonly object InitLock = new();
    private static IDataProtector? _protector;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        lock (InitLock)
        {
            if (_protector is not null)
                return;

            IDataProtectionProvider dataProtectionProvider =
                serviceProvider.GetRequiredService<IDataProtectionProvider>();
            _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        }
    }

    public static string EncryptToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return string.Empty;

        return EnsureInitialized().Protect(token);
    }

    public static string? DecryptToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            return EnsureInitialized().Unprotect(token);
        }
        catch (Exception)
        {
            // Return null — caller treats this as "no value" → triggers re-auth.
            // Never return raw ciphertext (leaks internal state, burns API rate limits).
            return null;
        }
    }

    // Bootstraps a Protector when called before the DI container exists (e.g. early
    // boot reads of the Configuration table). The keyring directory and application
    // name match ServiceConfiguration.ConfigureCoreServices so this standalone
    // provider and the DI-built provider share on-disk keys — ciphertext written by
    // either is decryptable by the other.
    private static IDataProtector EnsureInitialized()
    {
        if (_protector is not null)
            return _protector;

        lock (InitLock)
        {
            if (_protector is not null)
                return _protector;

            if (!Directory.Exists(AppFiles.DataProtectionKeysDir))
                Directory.CreateDirectory(AppFiles.DataProtectionKeysDir);

            ServiceCollection services = new();
            services
                .AddDataProtection()
                .PersistKeysToFileSystem(new(AppFiles.DataProtectionKeysDir))
                .SetApplicationName(ApplicationName);

            ServiceProvider provider = services.BuildServiceProvider();
            IDataProtectionProvider dataProtectionProvider =
                provider.GetRequiredService<IDataProtectionProvider>();
            _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
            return _protector;
        }
    }
}
