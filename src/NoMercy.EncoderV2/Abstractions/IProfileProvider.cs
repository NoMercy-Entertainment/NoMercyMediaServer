namespace NoMercy.EncoderV2.Abstractions;

/// <summary>
/// Provides encoding profiles
/// </summary>
public interface IProfileProvider
{
    /// <summary>
    /// Gets all available profiles from this provider
    /// </summary>
    Task<IReadOnlyList<IEncodingProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a profile by ID
    /// </summary>
    Task<IEncodingProfile?> GetProfileAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this provider supports saving profiles
    /// </summary>
    bool CanSaveProfiles { get; }

    /// <summary>
    /// Saves a profile (if supported)
    /// </summary>
    Task<bool> SaveProfileAsync(IEncodingProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a profile (if supported)
    /// </summary>
    Task<bool> DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registry for managing multiple profile providers
/// </summary>
public interface IProfileRegistry
{
    /// <summary>
    /// Gets all available profiles from all providers
    /// </summary>
    Task<IReadOnlyList<IEncodingProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a profile by ID (searches all providers)
    /// </summary>
    Task<IEncodingProfile?> GetProfileAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all system profiles
    /// </summary>
    Task<IReadOnlyList<IEncodingProfile>> GetSystemProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all user profiles
    /// </summary>
    Task<IReadOnlyList<IEncodingProfile>> GetUserProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a user profile
    /// </summary>
    Task<bool> SaveUserProfileAsync(IEncodingProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user profile
    /// </summary>
    Task<bool> DeleteUserProfileAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a profile provider
    /// </summary>
    void RegisterProvider(IProfileProvider provider);
}

/// <summary>
/// Factory for creating codec instances
/// </summary>
public interface ICodecFactory
{
    /// <summary>
    /// Creates a video codec by name
    /// </summary>
    IVideoCodec? CreateVideoCodec(string name);

    /// <summary>
    /// Creates an audio codec by name
    /// </summary>
    IAudioCodec? CreateAudioCodec(string name);

    /// <summary>
    /// Creates a subtitle codec by name
    /// </summary>
    ISubtitleCodec? CreateSubtitleCodec(string name);

    /// <summary>
    /// Gets all available video codecs
    /// </summary>
    IReadOnlyList<string> AvailableVideoCodecs { get; }

    /// <summary>
    /// Gets all available audio codecs
    /// </summary>
    IReadOnlyList<string> AvailableAudioCodecs { get; }

    /// <summary>
    /// Gets all available subtitle codecs
    /// </summary>
    IReadOnlyList<string> AvailableSubtitleCodecs { get; }
}

/// <summary>
/// Factory for creating container instances
/// </summary>
public interface IContainerFactory
{
    /// <summary>
    /// Creates a container by format name
    /// </summary>
    IContainer? CreateContainer(string formatName);

    /// <summary>
    /// Gets all available containers
    /// </summary>
    IReadOnlyList<string> AvailableContainers { get; }
}

/// <summary>
/// Hardware acceleration detection service
/// </summary>
public interface IHardwareAccelerationDetector
{
    /// <summary>
    /// Gets available hardware acceleration methods
    /// </summary>
    Task<IReadOnlyList<HardwareAcceleration>> GetAvailableAcceleratorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific acceleration method is available
    /// </summary>
    Task<bool> IsAvailableAsync(HardwareAcceleration acceleration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the recommended acceleration method for the current system
    /// </summary>
    Task<HardwareAcceleration?> GetRecommendedAsync(CancellationToken cancellationToken = default);
}
