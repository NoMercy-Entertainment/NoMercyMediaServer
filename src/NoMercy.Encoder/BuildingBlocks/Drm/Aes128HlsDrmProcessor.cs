// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using NoMercy.NmSystem.Security;
using NoMercy.Storage;

namespace NoMercy.Encoder.BuildingBlocks.Drm;

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
///
/// The artifacts are written to a per-encode directory under
/// <see cref="StoragePaths.TempRoot"/> — NEVER <paramref name="outputDirectory"/>
/// as passed to <see cref="PrepareAsync"/> — because that directory is the
/// encode's working dir and gets published to the served destination.
/// Shipping <c>drm.key</c> there would put the raw decryption key right next
/// to the ciphertext it protects. <see cref="Pipeline.Stages.ExecuteStage"/>
/// deletes the temp directory once ffmpeg has consumed it. The raw key is
/// also persisted protected (see <see cref="DrmKeyStore"/>) so an authorized
/// key-serving endpoint can still hand it to real clients.
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

        string tempDirectory = Path.Combine(
            StoragePaths.TempRoot,
            "drm-keys",
            Guid.NewGuid().ToString("N")
        );
        storage.CreateDirectory(tempDirectory);

        string keyFilePath = Path.Combine(tempDirectory, KeyFileName);
        string keyInfoPath = Path.Combine(tempDirectory, KeyInfoFileName);

        await DrmKeyStore.StoreKeyAsync(config.KeyUri, key, iv, ct).ConfigureAwait(false);
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
