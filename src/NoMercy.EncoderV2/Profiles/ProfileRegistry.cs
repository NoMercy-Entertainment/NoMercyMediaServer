using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Profiles;

/// <summary>
/// Default implementation of IProfileRegistry
/// </summary>
public sealed class ProfileRegistry : IProfileRegistry
{
    private readonly List<IProfileProvider> _providers = [];
    private readonly object _lock = new();

    public ProfileRegistry()
    {
        // Register system profile provider by default
        RegisterProvider(new SystemProfileProvider());
    }

    public void RegisterProvider(IProfileProvider provider)
    {
        lock (_lock)
        {
            _providers.Add(provider);
        }
    }

    public async Task<IReadOnlyList<IEncodingProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        List<IEncodingProfile> profiles = [];

        IProfileProvider[] providers;
        lock (_lock)
        {
            providers = [.. _providers];
        }

        foreach (IProfileProvider provider in providers)
        {
            IReadOnlyList<IEncodingProfile> providerProfiles = await provider.GetProfilesAsync(cancellationToken);
            profiles.AddRange(providerProfiles);
        }

        return profiles;
    }

    public async Task<IEncodingProfile?> GetProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        IProfileProvider[] providers;
        lock (_lock)
        {
            providers = [.. _providers];
        }

        foreach (IProfileProvider provider in providers)
        {
            IEncodingProfile? profile = await provider.GetProfileAsync(profileId, cancellationToken);
            if (profile != null)
            {
                return profile;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<IEncodingProfile>> GetSystemProfilesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IEncodingProfile> all = await GetAllProfilesAsync(cancellationToken);
        return all.Where(p => p.IsSystem).ToList();
    }

    public async Task<IReadOnlyList<IEncodingProfile>> GetUserProfilesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IEncodingProfile> all = await GetAllProfilesAsync(cancellationToken);
        return all.Where(p => !p.IsSystem).ToList();
    }

    public async Task<bool> SaveUserProfileAsync(IEncodingProfile profile, CancellationToken cancellationToken = default)
    {
        IProfileProvider[] providers;
        lock (_lock)
        {
            providers = [.. _providers];
        }

        foreach (IProfileProvider provider in providers)
        {
            if (provider.CanSaveProfiles)
            {
                return await provider.SaveProfileAsync(profile, cancellationToken);
            }
        }

        return false;
    }

    public async Task<bool> DeleteUserProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        IProfileProvider[] providers;
        lock (_lock)
        {
            providers = [.. _providers];
        }

        foreach (IProfileProvider provider in providers)
        {
            if (provider.CanSaveProfiles)
            {
                return await provider.DeleteProfileAsync(profileId, cancellationToken);
            }
        }

        return false;
    }
}

/// <summary>
/// Provides built-in system profiles
/// </summary>
public sealed class SystemProfileProvider : IProfileProvider
{
    public bool CanSaveProfiles => false;

    public Task<IReadOnlyList<IEncodingProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SystemProfiles.All);
    }

    public Task<IEncodingProfile?> GetProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        IEncodingProfile? profile = SystemProfiles.All.FirstOrDefault(p => p.Id == profileId);
        return Task.FromResult(profile);
    }

    public Task<bool> SaveProfileAsync(IEncodingProfile profile, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
