using NoMercy.Database.Models;
using NoMercy.EncoderV2.Repositories;

namespace NoMercy.EncoderV2.Profiles;

/// <summary>
/// Manages encoding profiles with validation and business logic
/// Provides high-level operations for profile management
/// </summary>
public interface IProfileManager
{
    Task<EncoderProfile?> GetProfileAsync(Ulid profileId);
    Task<List<EncoderProfile>> GetAllProfilesAsync();
    Task<EncoderProfile> CreateProfileAsync(EncoderProfile profile);
    Task UpdateProfileAsync(EncoderProfile profile);
    Task DeleteProfileAsync(Ulid profileId);
    Task<EncoderProfile?> GetDefaultProfileAsync();
    Task<ProfileValidationResult> ValidateProfileAsync(EncoderProfile profile);
}

/// <summary>
/// ProfileManager implementation with validation
/// </summary>
public class ProfileManager(IProfileRepository repository, IProfileValidator validator) : IProfileManager
{
    private readonly IProfileRepository _repository = repository;
    private readonly IProfileValidator _validator = validator;

    public async Task<EncoderProfile?> GetProfileAsync(Ulid profileId)
    {
        return await _repository.GetProfileAsync(profileId);
    }

    public async Task<List<EncoderProfile>> GetAllProfilesAsync()
    {
        return await _repository.GetAllProfilesAsync();
    }

    public async Task<EncoderProfile> CreateProfileAsync(EncoderProfile profile)
    {
        ProfileValidationResult validationResult = await _validator.ValidateAsync(profile);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Profile validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        return await _repository.CreateProfileAsync(profile);
    }

    public async Task UpdateProfileAsync(EncoderProfile profile)
    {
        bool exists = await _repository.ProfileExistsAsync(profile.Id);
        if (!exists)
        {
            throw new InvalidOperationException($"Profile with ID {profile.Id} not found");
        }

        ProfileValidationResult validationResult = await _validator.ValidateAsync(profile);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Profile validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        await _repository.UpdateProfileAsync(profile);
    }

    public async Task DeleteProfileAsync(Ulid profileId)
    {
        bool exists = await _repository.ProfileExistsAsync(profileId);
        if (!exists)
        {
            throw new InvalidOperationException($"Profile with ID {profileId} not found");
        }

        await _repository.DeleteProfileAsync(profileId);
    }

    public async Task<EncoderProfile?> GetDefaultProfileAsync()
    {
        return await _repository.GetDefaultProfileAsync();
    }

    public async Task<ProfileValidationResult> ValidateProfileAsync(EncoderProfile profile)
    {
        return await _validator.ValidateAsync(profile);
    }
}

