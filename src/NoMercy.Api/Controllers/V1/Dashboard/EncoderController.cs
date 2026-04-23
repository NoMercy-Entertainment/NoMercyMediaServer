using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.Profiles;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Encoder Profiles")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoderprofiles", Order = 10)]
public class EncoderController(EncoderRepository encoderRepository, CodecRegistry codecRegistry)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder profiles");

        List<EncoderProfile> encoderProfiles = await encoderRepository.GetEncoderProfilesAsync();

        return Ok(encoderProfiles);
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create encoder profiles");

        try
        {
            int encoderProfiles = await encoderRepository.GetEncoderProfileCountAsync();

            Ulid profileId = Ulid.NewUlid();
            EncodingProfile v3Profile = new(
                Id: profileId,
                Name: $"Profile {encoderProfiles}",
                Format: OutputFormat.Hls,
                VideoOutputs:
                [
                    new(
                        Codec: VideoCodecType.H264,
                        Width: 1920,
                        Height: null,
                        BitrateKbps: 8000,
                        Crf: 23,
                        Preset: "medium",
                        Profile: "high",
                        Level: "4.0",
                        ConvertHdrToSdr: true,
                        KeyframeIntervalSeconds: 2,
                        TenBit: false
                    ),
                ],
                AudioOutputs:
                [
                    new(
                        Codec: AudioCodecType.Aac,
                        BitrateKbps: 192,
                        Channels: 2,
                        SampleRateHz: 48000,
                        AllowedLanguages: [],
                        Loudness: NoMercy.Encoder.Audio.LoudnessMode.None
                    ),
                ],
                SubtitleOutputs: []
            );

            EncoderProfile profile = new()
            {
                Id = profileId,
                Name = $"Profile {encoderProfiles}",
                Container = "m3u8",
                Param = JsonConvert.SerializeObject(v3Profile),
            };

            await encoderRepository.AddEncoderProfileAsync(profile);

            return Ok(
                new StatusResponseDto<EncoderProfile>
                {
                    Status = "ok",
                    Data = profile,
                    Message = "Successfully created a new encoder profile.",
                    Args = [],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse("Something went wrong creating the encoder profile");
        }
    }

    [HttpDelete]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Destroy(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to remove encoder profiles");

        EncoderProfile? profile = await encoderRepository.GetEncoderProfileByIdAsync(id);

        if (profile == null)
            return NotFoundResponse("Encoder profile not found");

        await encoderRepository.DeleteEncoderProfileAsync(profile);

        return Ok(new StatusResponseDto<string> { Status = "ok", Data = "Profile removed" });
    }

    [HttpGet]
    [Route("containers")]
    public IActionResult Containers()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder profiles");

        ContainerDto[] containers =
        [
            BuildContainer("HLS (Streaming)", "m3u8", "hls", true, codecRegistry),
            BuildContainer("MKV (Matroska)", "mkv", "mkv", false, codecRegistry),
            BuildContainer("MP4", "mp4", "mp4", false, codecRegistry),
        ];

        return Ok(new DataResponseDto<ContainerDto[]> { Data = containers });
    }

    [HttpGet]
    [Route("framesizes")]
    public IActionResult FrameSizes()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder profiles");

        VideoQualityDto[] frameSizes =
        [
            new()
            {
                Width = 7680,
                Height = 4320,
                Label = "8K (7680x4320)",
            },
            new()
            {
                Width = 3840,
                Height = 2160,
                Label = "4K (3840x2160)",
            },
            new()
            {
                Width = 2560,
                Height = 1440,
                Label = "1440p (2560x1440)",
            },
            new()
            {
                Width = 1920,
                Height = 1080,
                Label = "1080p (1920x1080)",
            },
            new()
            {
                Width = 1280,
                Height = 720,
                Label = "720p (1280x720)",
            },
            new()
            {
                Width = 854,
                Height = 480,
                Label = "480p (854x480)",
            },
            new()
            {
                Width = 640,
                Height = 360,
                Label = "360p (640x360)",
            },
        ];

        return Ok(new DataResponseDto<VideoQualityDto[]> { Data = frameSizes });
    }

    private static ContainerDto BuildContainer(
        string label,
        string value,
        string type,
        bool isDefault,
        CodecRegistry registry
    )
    {
        VideoCodecType[] videoTypes =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ];

        VideoCodecDto[] videoCodecs = videoTypes
            .Select(vt =>
            {
                ICodecDefinition def = registry.GetVideoDefinition(vt);
                EncoderInfo sw = def.Encoders.First(e => e.RequiredVendor is null);

                return new VideoCodecDto
                {
                    Name = vt.ToString(),
                    Value = sw.FfmpegName,
                    SimpleValue = vt.ToString().ToLowerInvariant(),
                    RequiresGpu = false,
                    IsDefault = vt == VideoCodecType.H264,
                    AvailablePresets = sw.Presets.Select(p => new LabelValueDto(p)).ToArray(),
                    AvailableVideoProfiles = sw
                        .Profiles.Select(p => new LabelValueDto(p))
                        .ToArray(),
                };
            })
            .ToArray();

        AudioCodecType[] audioTypes =
        [
            AudioCodecType.Aac,
            AudioCodecType.Opus,
            AudioCodecType.Flac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Mp3,
        ];

        AudioCodecDto[] audioCodecs = audioTypes
            .Select(at =>
            {
                AudioEncoderInfo enc = AudioCodecDefinitions.GetEncoder(at);
                return new AudioCodecDto
                {
                    Name = at.ToString(),
                    Value = enc.FfmpegName,
                    SimpleValue = at.ToString().ToLowerInvariant(),
                    IsDefault = at == AudioCodecType.Aac,
                };
            })
            .ToArray();

        SubtitleCodecDto[] subtitleCodecs =
        [
            new()
            {
                Name = "WebVTT",
                Value = "webvtt",
                SimpleValue = "webvtt",
                IsDefault = true,
            },
            new()
            {
                Name = "ASS",
                Value = "ass",
                SimpleValue = "ass",
            },
            new()
            {
                Name = "SRT",
                Value = "srt",
                SimpleValue = "srt",
            },
        ];

        return new()
        {
            Label = label,
            Value = value,
            Type = type,
            IsDefault = isDefault,
            AvailableVideoCodecs = videoCodecs,
            AvailableAudioCodecs = audioCodecs,
            AvailableSubtitleCodecs = subtitleCodecs,
            AvailableVideoSizes =
            [
                new()
                {
                    Width = 3840,
                    Height = 2160,
                    Label = "4K",
                },
                new()
                {
                    Width = 1920,
                    Height = 1080,
                    Label = "1080p",
                },
                new()
                {
                    Width = 1280,
                    Height = 720,
                    Label = "720p",
                },
                new()
                {
                    Width = 854,
                    Height = 480,
                    Label = "480p",
                },
            ],
        };
    }
}
