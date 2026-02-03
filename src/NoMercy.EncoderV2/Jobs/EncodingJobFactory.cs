using System.Security.Cryptography;
using NoMercy.EncoderV2.Shared.Dtos;

namespace NoMercy.EncoderV2.Jobs;

public class EncodingJobFactory
{
    private readonly string _ffmpegPath;
    private readonly AllCapabilities _capabilities;

    public EncodingJobFactory(string ffmpegPath, AllCapabilities capabilities)
    {
        _ffmpegPath = ffmpegPath;
        _capabilities = capabilities;
    }

    public async Task<EncodingJobPayload> CreateJobAsync(
        string inputPath,
        string outputFolder,
        string outputFileName,
        EncodingProfile? profile = null,
        string? networkPath = null)
    {
        FileInfo fileInfo = new(inputPath);
        string fileHash = await ComputeFileHash(inputPath);

        EncodingJobPayload job = new()
        {
            JobId = Guid.NewGuid().ToString(),
            MediaType = DetermineMediaType(profile),
            Input = new()
            {
                FilePath = inputPath,
                NetworkPath = networkPath,
                FileHash = fileHash,
                FileSize = fileInfo.Length,
                Duration = GetMediaDuration(inputPath)
            },
            Output = new()
            {
                DestinationFolder = outputFolder,
                FileName = outputFileName
            },
            Profile = profile
        };

        return job;
    }

    private string DetermineMediaType(EncodingProfile? profile)
    {
        // TODO: Implement profile type detection based on profile properties
        return profile?.VideoProfile != null ? "video" : profile?.AudioProfile != null ? "audio" : "video";
    }

    private async Task<string> ComputeFileHash(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }

    private TimeSpan GetMediaDuration(string filePath)
    {
        // TODO: Implement ffprobe duration extraction
        return TimeSpan.Zero;
    }
}
