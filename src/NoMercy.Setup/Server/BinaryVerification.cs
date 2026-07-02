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

using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Serilog.Events;

namespace NoMercy.Setup.Server;

/// <summary>
/// Represents a single asset entry inside a NoMercy release manifest.
/// </summary>
public sealed class ManifestAsset
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonProperty("size")]
    public long Size { get; set; }
}

/// <summary>
/// Represents the release manifest produced by the CI pipeline.
/// </summary>
public sealed class ReleaseManifest
{
    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("commit_sha")]
    public string CommitSha { get; set; } = string.Empty;

    [JsonProperty("build_timestamp")]
    public string BuildTimestamp { get; set; } = string.Empty;

    [JsonProperty("assets")]
    public ManifestAsset[] Assets { get; set; } = [];
}

/// <summary>
/// Provides SHA-256 file verification and PGP detached-signature verification for
/// release manifests.  Both operations are purely computational and have no I/O
/// side-effects beyond reading the file bytes.
/// </summary>
public static class BinaryVerification
{
    // Embedded org public key.  Replace the placeholder content with the real
    // ASCII-armored PGP public key exported from the org's signing key.
    private static readonly string? EmbeddedPublicKey = LoadEmbeddedPublicKey();

    /// <summary>
    /// Computes the SHA-256 hash of <paramref name="filePath"/> and compares it against
    /// <paramref name="expectedHex"/> (case-insensitive, hex-encoded).
    /// </summary>
    /// <returns>
    /// <c>true</c> when hashes match; <c>false</c> when they differ.
    /// </returns>
    public static async Task<bool> VerifyFileSha256Async(
        string filePath,
        string expectedHex,
        CancellationToken ct = default
    )
    {
        await using FileStream fs = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true
        );
        byte[] hash = await SHA256.HashDataAsync(fs, ct);
        string actual = Convert.ToHexString(hash);
        return string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies a detached ASCII-armored PGP signature over <paramref name="manifestJson"/>
    /// using the org's embedded public key.
    /// </summary>
    /// <param name="manifestJson">The raw UTF-8 manifest text.</param>
    /// <param name="armoredSignature">The ASCII-armored detached signature (.sig file contents).</param>
    /// <returns>
    /// <c>true</c> on successful verification; <c>false</c> if the embedded key is
    /// unavailable or the signature does not match.
    /// </returns>
    public static bool VerifyManifestSignature(string manifestJson, string armoredSignature)
    {
        if (string.IsNullOrEmpty(EmbeddedPublicKey))
        {
            NmSystem.SystemCalls.Logger.Setup(
                "No embedded org public key — manifest signature not verified",
                LogEventLevel.Warning
            );
            return false;
        }

        return VerifyManifestSignature(manifestJson, armoredSignature, EmbeddedPublicKey);
    }

    /// <summary>
    /// Verifies a detached ASCII-armored PGP signature over <paramref name="manifestJson"/>
    /// using the supplied <paramref name="armoredPublicKey"/>.
    /// </summary>
    /// <remarks>
    /// This overload is exposed for testing with caller-supplied keys.
    /// </remarks>
    internal static bool VerifyManifestSignature(
        string manifestJson,
        string armoredSignature,
        string armoredPublicKey
    )
    {
        try
        {
            using MemoryStream keyStream = new(
                System.Text.Encoding.ASCII.GetBytes(armoredPublicKey)
            );
            using MemoryStream sigStream = new(
                System.Text.Encoding.ASCII.GetBytes(armoredSignature)
            );
            using MemoryStream dataStream = new(System.Text.Encoding.UTF8.GetBytes(manifestJson));

            using ArmoredInputStream armoredKey = new(keyStream);
            PgpPublicKeyRingBundle keyRingBundle = new(armoredKey);

            using ArmoredInputStream armoredSig = new(sigStream);
            PgpObjectFactory sigFactory = new(armoredSig);

            PgpSignatureList? signatureList = null;
            PgpObject? sigObj = sigFactory.NextPgpObject();
            while (sigObj is not null)
            {
                if (sigObj is PgpSignatureList sl)
                {
                    signatureList = sl;
                    break;
                }

                sigObj = sigFactory.NextPgpObject();
            }

            if (signatureList is null || signatureList.Count == 0)
                return false;

            for (int i = 0; i < signatureList.Count; i++)
            {
                PgpSignature signature = signatureList[i];
                PgpPublicKey? pubKey = keyRingBundle.GetPublicKey(signature.KeyId);
                if (pubKey is null)
                    continue;

                signature.InitVerify(pubKey);

                byte[] data = System.Text.Encoding.UTF8.GetBytes(manifestJson);
                signature.Update(data, 0, data.Length);

                if (signature.Verify())
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            NmSystem.SystemCalls.Logger.Setup(
                $"Manifest signature verification error: {ex.Message}",
                LogEventLevel.Warning
            );
            return false;
        }
    }

    private static string? LoadEmbeddedPublicKey()
    {
        try
        {
            Assembly assembly = typeof(BinaryVerification).Assembly;
            string resourceName = "NoMercy.Setup.Resources.nomercy-public-key.asc";
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return null;

            using StreamReader reader = new(stream);
            string content = reader.ReadToEnd().Trim();

            // Treat a placeholder/empty file as "not configured"
            if (
                string.IsNullOrWhiteSpace(content)
                || content.StartsWith("# PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
                || !content.Contains("BEGIN PGP PUBLIC KEY BLOCK", StringComparison.Ordinal)
            )
                return null;

            return content;
        }
        catch
        {
            return null;
        }
    }
}
