namespace NoMercy.Encoder.BuildingBlocks.Drm;

using System.Security.Cryptography;
using System.Text;
using NoMercy.Storage;

/// <summary>
/// AES-128 HLS encryption. Writes a <c>key</c> file containing the raw
/// 16-byte key and a <c>keyinfo.txt</c> file in the format ffmpeg's HLS
/// muxer expects when given <c>-hls_key_info_file</c>:
///
/// <code>
/// key URI (URL clients fetch)
/// key file path (local, fed to ffmpeg)
/// IV hex (optional)
/// </code>
///
/// Ffmpeg reads this, encrypts each segment with AES-128-CBC, and emits
/// <c>#EXT-X-KEY:METHOD=AES-128,URI="&lt;uri&gt;",IV=0x&lt;iv&gt;</c>
/// into the playlist automatically.
/// </summary>
public class Aes128HlsDrmProcessor(IStorage storage) : IDrmProcessor
{
    private const string KeyFileName = "drm.key";
    private const string KeyInfoFileName = "drm_keyinfo.txt";

    public DrmMethod Method => DrmMethod.Aes128;

    public async Task<DrmArtifact> PrepareAsync(
        string outputDirectory,
        DrmConfig config,
        CancellationToken ct
    )
    {
        if (config.Method != DrmMethod.Aes128)
            throw new ArgumentException(
                $"This processor handles AES-128 only, got {config.Method}",
                nameof(config)
            );

        if (string.IsNullOrWhiteSpace(config.KeyUri))
            throw new ArgumentException("DrmConfig.KeyUri is required", nameof(config));

        storage.CreateDirectory(outputDirectory);

        byte[] key = config.Key ?? RandomNumberGenerator.GetBytes(16);
        if (key.Length != 16)
            throw new ArgumentException(
                $"AES-128 key must be 16 bytes, got {key.Length}",
                nameof(config)
            );

        byte[] iv = config.Iv ?? RandomNumberGenerator.GetBytes(16);
        if (iv.Length != 16)
            throw new ArgumentException(
                $"AES-128 IV must be 16 bytes, got {iv.Length}",
                nameof(config)
            );

        string keyFilePath = Path.Combine(outputDirectory, KeyFileName);
        string keyInfoPath = Path.Combine(outputDirectory, KeyInfoFileName);

        await storage.WriteAsync(keyFilePath, key, ct).ConfigureAwait(false);

        // ffmpeg accepts forward slashes everywhere; normalizing keeps tests
        // stable on Windows where Path.Combine yields backslashes.
        string keyFileForwardSlash = keyFilePath.Replace('\\', '/');
        string keyInfoContent =
            $"{config.KeyUri}\n{keyFileForwardSlash}\n{Convert.ToHexString(iv).ToLowerInvariant()}\n";
        await storage
            .WriteAsync(keyInfoPath, Encoding.UTF8.GetBytes(keyInfoContent), ct)
            .ConfigureAwait(false);

        return new(
            KeyInfoFilePath: keyInfoPath,
            KeyFilePath: keyFilePath,
            KeyUri: config.KeyUri,
            Key: key,
            Iv: iv
        );
    }
}
