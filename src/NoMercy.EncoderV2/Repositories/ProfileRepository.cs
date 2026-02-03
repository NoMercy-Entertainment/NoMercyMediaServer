using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Repositories;

/// <summary>
/// Repository for EncoderProfile operations with MediaContext
/// Provides CRUD operations for encoding profiles
/// </summary>
public interface IProfileRepository
{
    Task<EncoderProfile?> GetProfileAsync(Ulid profileId);
    Task<List<EncoderProfile>> GetAllProfilesAsync();
    Task<List<EncoderProfile>> ListProfilesAsync();
    Task<EncoderProfile> CreateProfileAsync(EncoderProfile profile);
    Task<EncoderProfile> UpdateProfileAsync(EncoderProfile profile);
    Task DeleteProfileAsync(Ulid profileId);
    Task<bool> ProfileExistsAsync(Ulid profileId);
    Task<EncoderProfile?> GetDefaultProfileAsync();
}

/// <summary>
/// Profile repository implementation using MediaContext
/// </summary>
public class ProfileRepository(MediaContext context) : IProfileRepository
{
    private readonly MediaContext _context = context;

    public async Task<EncoderProfile?> GetProfileAsync(Ulid profileId)
    {
        return await _context.EncoderProfiles
            .Include(p => p.EncoderProfileFolder)
            .FirstOrDefaultAsync(p => p.Id == profileId);
    }

    public async Task<List<EncoderProfile>> GetAllProfilesAsync()
    {
        return await _context.EncoderProfiles
            .Include(p => p.EncoderProfileFolder)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<EncoderProfile>> ListProfilesAsync()
    {
        return await GetAllProfilesAsync();
    }

    public async Task<EncoderProfile> CreateProfileAsync(EncoderProfile profile)
    {
        profile.Id = Ulid.NewUlid();
        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        _context.EncoderProfiles.Add(profile);
        await _context.SaveChangesAsync();

        return profile;
    }

    public async Task<EncoderProfile> UpdateProfileAsync(EncoderProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;

        _context.EncoderProfiles.Update(profile);
        await _context.SaveChangesAsync();

        return profile;
    }

    public async Task DeleteProfileAsync(Ulid profileId)
    {
        EncoderProfile? profile = await _context.EncoderProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId);

        if (profile != null)
        {
            _context.EncoderProfiles.Remove(profile);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ProfileExistsAsync(Ulid profileId)
    {
        return await _context.EncoderProfiles
            .AnyAsync(p => p.Id == profileId);
    }

    public async Task<EncoderProfile?> GetDefaultProfileAsync()
    {
        return await _context.EncoderProfiles
            .Include(p => p.EncoderProfileFolder)
            .FirstOrDefaultAsync(p => p.Name == "Default" || p.Name == "1080p high");
    }
}
