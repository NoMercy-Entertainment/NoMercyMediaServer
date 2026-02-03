using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Codecs.Audio;
using NoMercy.EncoderV2.Codecs.Subtitle;
using NoMercy.EncoderV2.Codecs.Video;
using NoMercy.EncoderV2.Containers;
using NoMercy.EncoderV2.Profiles;

namespace NoMercy.Tests.EncoderV2.Profiles;

public class ProfileTests
{
    #region EncodingProfile Validation Tests

    [Fact]
    public void EncodingProfile_ValidProfile_ValidationSucceeds()
    {
        EncodingProfile profile = CreateValidProfile();

        ValidationResult result = profile.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void EncodingProfile_MissingVideoCodec_ValidationFails()
    {
        EncodingProfile profile = new()
        {
            Id = "test",
            Name = "Test Profile",
            Container = new HlsContainer(),
            VideoOutputs =
            [
                new VideoOutputConfig
                {
                    Id = "main",
                    Codec = null!
                }
            ]
        };

        ValidationResult result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no codec"));
    }

    [Fact]
    public void EncodingProfile_InvalidCodecSettings_ValidationFails()
    {
        EncodingProfile profile = new()
        {
            Id = "test",
            Name = "Test Profile",
            Container = new HlsContainer(),
            VideoOutputs =
            [
                new VideoOutputConfig
                {
                    Id = "main",
                    Codec = new H264Codec { Crf = 100 } // Invalid CRF
                }
            ]
        };

        ValidationResult result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("CRF"));
    }

    [Fact]
    public void EncodingProfile_IncompatibleContainerCodec_ValidationFails()
    {
        EncodingProfile profile = new()
        {
            Id = "test",
            Name = "Test Profile",
            Container = new WebMContainer(),
            VideoOutputs =
            [
                new VideoOutputConfig
                {
                    Id = "main",
                    Codec = new H264Codec() // H.264 not compatible with WebM
                }
            ]
        };

        ValidationResult result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("only supports"));
    }

    [Fact]
    public void EncodingProfile_InvalidThumbnailConfig_ValidationFails()
    {
        EncodingProfile profile = new()
        {
            Id = "test",
            Name = "Test Profile",
            Container = new Mp4Container(),
            ThumbnailConfig = new ThumbnailConfig
            {
                IntervalSeconds = -1, // Invalid
                Width = 320,
                Quality = 75
            }
        };

        ValidationResult result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("interval"));
    }

    [Fact]
    public void EncodingProfile_InvalidThumbnailWidth_ValidationFails()
    {
        EncodingProfile profile = new()
        {
            Id = "test",
            Name = "Test Profile",
            Container = new Mp4Container(),
            ThumbnailConfig = new ThumbnailConfig
            {
                IntervalSeconds = 10,
                Width = 0, // Invalid
                Quality = 75
            }
        };

        ValidationResult result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("width"));
    }

    [Fact]
    public void EncodingProfile_InvalidThumbnailQuality_ValidationFails()
    {
        EncodingProfile profile = new()
        {
            Id = "test",
            Name = "Test Profile",
            Container = new Mp4Container(),
            ThumbnailConfig = new ThumbnailConfig
            {
                IntervalSeconds = 10,
                Width = 320,
                Quality = 150 // Invalid
            }
        };

        ValidationResult result = profile.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("quality"));
    }

    #endregion

    #region SystemProfiles Tests

    [Fact]
    public void SystemProfiles_HlsAdaptive_IsValid()
    {
        IEncodingProfile profile = SystemProfiles.HlsAdaptive;

        ValidationResult result = profile.Validate();

        Assert.True(result.IsValid);
        Assert.True(profile.IsSystem);
        Assert.Equal("hls-adaptive", profile.Id);
    }

    [Fact]
    public void SystemProfiles_HlsHevc_IsValid()
    {
        IEncodingProfile profile = SystemProfiles.HlsHevc;

        ValidationResult result = profile.Validate();

        Assert.True(result.IsValid);
        Assert.True(profile.IsSystem);
    }

    [Fact]
    public void SystemProfiles_Hls4K_IsValid()
    {
        IEncodingProfile profile = SystemProfiles.Hls4K;

        ValidationResult result = profile.Validate();

        Assert.True(result.IsValid);
        Assert.True(profile.IsSystem);
    }

    [Fact]
    public void SystemProfiles_WebMp4_IsValid()
    {
        IEncodingProfile profile = SystemProfiles.WebMp4;

        ValidationResult result = profile.Validate();

        Assert.True(result.IsValid);
        Assert.True(profile.IsSystem);
    }

    [Fact]
    public void SystemProfiles_ArchiveMkv_IsValid()
    {
        IEncodingProfile profile = SystemProfiles.ArchiveMkv;

        ValidationResult result = profile.Validate();

        Assert.True(result.IsValid);
        Assert.True(profile.IsSystem);
    }

    [Fact]
    public void SystemProfiles_AudioOpus_IsValid()
    {
        IEncodingProfile profile = SystemProfiles.AudioOpus;

        ValidationResult result = profile.Validate();

        Assert.True(result.IsValid);
        Assert.True(profile.IsSystem);
        Assert.Empty(profile.VideoOutputs);
    }

    [Fact]
    public void SystemProfiles_All_ContainsExpectedCount()
    {
        IReadOnlyList<IEncodingProfile> all = SystemProfiles.All;

        Assert.Equal(6, all.Count);
    }

    [Fact]
    public void SystemProfiles_All_AreValid()
    {
        foreach (IEncodingProfile profile in SystemProfiles.All)
        {
            ValidationResult result = profile.Validate();
            Assert.True(result.IsValid, $"Profile {profile.Id} failed validation: {string.Join(", ", result.Errors)}");
        }
    }

    [Fact]
    public void SystemProfiles_HlsAdaptive_HasMultipleResolutions()
    {
        IEncodingProfile profile = SystemProfiles.HlsAdaptive;

        Assert.True(profile.VideoOutputs.Count >= 3);
        Assert.Contains(profile.VideoOutputs, v => v.Id == "1080p");
        Assert.Contains(profile.VideoOutputs, v => v.Id == "720p");
        Assert.Contains(profile.VideoOutputs, v => v.Id == "480p");
    }

    #endregion

    #region ProfileRegistry Tests

    [Fact]
    public async Task ProfileRegistry_GetAllProfiles_ReturnsSystemProfiles()
    {
        ProfileRegistry registry = new();

        IReadOnlyList<IEncodingProfile> profiles = await registry.GetAllProfilesAsync();

        Assert.NotEmpty(profiles);
        Assert.All(profiles, p => Assert.True(p.IsSystem));
    }

    [Fact]
    public async Task ProfileRegistry_GetProfile_ReturnsCorrectProfile()
    {
        ProfileRegistry registry = new();

        IEncodingProfile? profile = await registry.GetProfileAsync("hls-adaptive");

        Assert.NotNull(profile);
        Assert.Equal("hls-adaptive", profile.Id);
    }

    [Fact]
    public async Task ProfileRegistry_GetProfile_NonExistent_ReturnsNull()
    {
        ProfileRegistry registry = new();

        IEncodingProfile? profile = await registry.GetProfileAsync("non-existent");

        Assert.Null(profile);
    }

    [Fact]
    public async Task ProfileRegistry_GetSystemProfiles_ReturnsOnlySystemProfiles()
    {
        ProfileRegistry registry = new();

        IReadOnlyList<IEncodingProfile> profiles = await registry.GetSystemProfilesAsync();

        Assert.NotEmpty(profiles);
        Assert.All(profiles, p => Assert.True(p.IsSystem));
    }

    [Fact]
    public async Task ProfileRegistry_GetUserProfiles_InitiallyEmpty()
    {
        ProfileRegistry registry = new();

        IReadOnlyList<IEncodingProfile> profiles = await registry.GetUserProfilesAsync();

        Assert.Empty(profiles);
    }

    #endregion

    #region SystemProfileProvider Tests

    [Fact]
    public void SystemProfileProvider_CanSaveProfiles_ReturnsFalse()
    {
        SystemProfileProvider provider = new();

        Assert.False(provider.CanSaveProfiles);
    }

    [Fact]
    public async Task SystemProfileProvider_SaveProfile_ReturnsFalse()
    {
        SystemProfileProvider provider = new();
        EncodingProfile profile = CreateValidProfile();

        bool result = await provider.SaveProfileAsync(profile);

        Assert.False(result);
    }

    [Fact]
    public async Task SystemProfileProvider_DeleteProfile_ReturnsFalse()
    {
        SystemProfileProvider provider = new();

        bool result = await provider.DeleteProfileAsync("any-id");

        Assert.False(result);
    }

    #endregion

    private static EncodingProfile CreateValidProfile()
    {
        return new EncodingProfile
        {
            Id = "test-profile",
            Name = "Test Profile",
            Description = "A test profile",
            IsSystem = false,
            Container = new HlsContainer(),
            VideoOutputs =
            [
                new VideoOutputConfig
                {
                    Id = "1080p",
                    Codec = new H264Codec { Preset = "medium", Crf = 23 },
                    Width = 1920,
                    Height = 1080
                }
            ],
            AudioOutputs =
            [
                new AudioOutputConfig
                {
                    Id = "stereo",
                    Codec = new AacCodec { Bitrate = 128, Channels = 2 }
                }
            ],
            SubtitleOutputs =
            [
                new SubtitleOutputConfig
                {
                    Id = "webvtt",
                    Codec = new WebvttCodec()
                }
            ],
            Options = new EncodingOptions
            {
                UseHardwareAcceleration = true,
                OverwriteOutput = true
            }
        };
    }
}
