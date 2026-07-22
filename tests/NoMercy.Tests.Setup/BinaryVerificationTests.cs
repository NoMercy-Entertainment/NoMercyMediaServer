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
using Newtonsoft.Json;
using NoMercy.Setup.Server;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace NoMercy.Tests.Setup;

[Trait(name: "Category", value: "Unit")]
public class BinaryVerificationTests : IDisposable
{
    private readonly string _tempDir;

    public BinaryVerificationTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"nomercy-bv-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // SHA-256 verification
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyFileSha256_CorrectHash_ReturnsTrue()
    {
        byte[] content = Encoding.UTF8.GetBytes(s: "hello nomercy");
        string filePath = Path.Combine(path1: _tempDir, path2: "test.bin");
        await File.WriteAllBytesAsync(path: filePath, bytes: content);

        string expected = Convert.ToHexString(inArray: SHA256.HashData(source: content));

        bool result = await BinaryVerification.VerifyFileSha256Async(filePath: filePath, expectedHex: expected);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyFileSha256_WrongHash_ReturnsFalse()
    {
        byte[] content = Encoding.UTF8.GetBytes(s: "hello nomercy");
        string filePath = Path.Combine(path1: _tempDir, path2: "test.bin");
        await File.WriteAllBytesAsync(path: filePath, bytes: content);

        string wrong = new(c: '0', count: 64);

        bool result = await BinaryVerification.VerifyFileSha256Async(filePath: filePath, expectedHex: wrong);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyFileSha256_HashCaseInsensitive_ReturnsTrue()
    {
        byte[] content = Encoding.UTF8.GetBytes(s: "case test");
        string filePath = Path.Combine(path1: _tempDir, path2: "case.bin");
        await File.WriteAllBytesAsync(path: filePath, bytes: content);

        string expectedUpper = Convert.ToHexString(inArray: SHA256.HashData(source: content));
        string expectedLower = expectedUpper.ToLowerInvariant();

        bool upper = await BinaryVerification.VerifyFileSha256Async(filePath: filePath, expectedHex: expectedUpper);
        bool lower = await BinaryVerification.VerifyFileSha256Async(filePath: filePath, expectedHex: expectedLower);

        upper.Should().BeTrue();
        lower.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // GitHub asset digest extraction
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(data: ["sha256:ABC123", "ABC123"])]
    [InlineData(data: ["SHA256:deadbeef", "deadbeef"])]
    public void ExtractSha256FromDigest_ValidPrefix_ReturnsHex(string digest, string expected)
    {
        BinaryVerification.ExtractSha256FromDigest(digest: digest).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "   ")]
    [InlineData(data: "sha512:abcdef")]
    [InlineData(data: "abcdef")]
    public void ExtractSha256FromDigest_MissingOrUnsupported_ReturnsNull(string? digest)
    {
        BinaryVerification.ExtractSha256FromDigest(digest: digest).Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Upstream SHA2-256SUMS parsing (yt-dlp and friends)
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseSha256Sums_FindsMatchingAsset()
    {
        string sums = "aaa111  yt-dlp\nbbb222  yt-dlp_linux\nccc333  yt-dlp.exe\n";

        BinaryVerification.ParseSha256Sums(sumsContent: sums, targetFileName: "yt-dlp_linux").Should().Be(expected: "bbb222");
    }

    [Fact]
    public void ParseSha256Sums_ToleratesBinaryMarkerAndBlankLines()
    {
        string sums = "\n   \ndeadbeef *yt-dlp_macos\n";

        BinaryVerification.ParseSha256Sums(sumsContent: sums, targetFileName: "yt-dlp_macos").Should().Be(expected: "deadbeef");
    }

    [Fact]
    public void ParseSha256Sums_NoMatch_ReturnsNull()
    {
        string sums = "aaa111  yt-dlp\nbbb222  yt-dlp_linux\n";

        BinaryVerification.ParseSha256Sums(sumsContent: sums, targetFileName: "yt-dlp_win.exe").Should().BeNull();
    }

    [Fact]
    public void ParseSha256Sums_Empty_ReturnsNull()
    {
        BinaryVerification.ParseSha256Sums(sumsContent: string.Empty, targetFileName: "anything").Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Manifest JSON parsing
    // -------------------------------------------------------------------------

    [Fact]
    public void ReleaseManifest_Deserializes_AllFields()
    {
        string json = """
            {
              "version": "1.2.3",
              "commit_sha": "abc1234",
              "build_timestamp": "2025-01-01T00:00:00Z",
              "assets": [
                { "name": "nomercy-linux-x64", "sha256": "deadbeef", "size": 42 }
              ]
            }
            """;

        ReleaseManifest? manifest = JsonConvert.DeserializeObject<ReleaseManifest>(value: json);

        manifest.Should().NotBeNull();
        manifest!.Version.Should().Be(expected: "1.2.3");
        manifest.CommitSha.Should().Be(expected: "abc1234");
        manifest.Assets.Should().HaveCount(expected: 1);
        manifest.Assets[0].Name.Should().Be(expected: "nomercy-linux-x64");
        manifest.Assets[0].Sha256.Should().Be(expected: "deadbeef");
        manifest.Assets[0].Size.Should().Be(expected: 42);
    }

    [Fact]
    public void ReleaseManifest_EmptyAssets_ParsesWithoutError()
    {
        string json = """{"version":"0.1","commit_sha":"x","build_timestamp":"t","assets":[]}""";

        ReleaseManifest? manifest = JsonConvert.DeserializeObject<ReleaseManifest>(value: json);

        manifest.Should().NotBeNull();
        manifest!.Assets.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // PGP manifest signature verification
    // -------------------------------------------------------------------------

    [Fact]
    public void VerifyManifestSignature_GarbageSignatureAgainstEmbeddedKey_ReturnsFalse()
    {
        // The public overload uses the real embedded org key. A malformed detached
        // signature must be rejected cleanly (false), never throw.
        bool result = BinaryVerification.VerifyManifestSignature(
            manifestJson: """{"version":"1.0"}""",
            armoredSignature: "-----BEGIN PGP SIGNATURE-----\nfake\n-----END PGP SIGNATURE-----"
        );

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyManifestSignature_ForeignSignatureAgainstEmbeddedKey_ReturnsFalse()
    {
        string manifest = """{"version":"1.0","assets":[]}""";
        (_, string foreignSignature) = GenerateSignedManifest(manifest: manifest);

        // A valid signature from a key that is NOT the embedded org key must not
        // verify — the embedded overload looks the signer up by key id and misses.
        bool result = BinaryVerification.VerifyManifestSignature(manifestJson: manifest, armoredSignature: foreignSignature);

        result.Should().BeFalse();
    }

    [Fact]
    public void EmbeddedPublicKey_IsRealKey_NotPlaceholder()
    {
        // Regression guard: the shipped assembly must embed the real org public key,
        // not the placeholder that silently disabled all signature verification.
        using Stream? stream = typeof(BinaryVerification).Assembly.GetManifestResourceStream(
            name: "NoMercy.Setup.Resources.nomercy-public-key.asc"
        );

        stream.Should().NotBeNull(because: "the org public key must be embedded for manifest verification");

        using StreamReader reader = new(stream: stream!);
        string content = reader.ReadToEnd();

        content.Should().Contain(expected: "BEGIN PGP PUBLIC KEY BLOCK");
        content.Should().NotContain(unexpected: "PLACEHOLDER");
    }

    [Fact]
    public void VerifyManifestSignature_ValidKeyAndSignature_ReturnsTrue()
    {
        string manifest = """{"version":"1.0","assets":[]}""";

        (string armoredPublicKey, string armoredSignature) = GenerateSignedManifest(manifest: manifest);

        bool result = BinaryVerification.VerifyManifestSignature(
            manifestJson: manifest,
            armoredSignature: armoredSignature,
            armoredPublicKey: armoredPublicKey
        );

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyManifestSignature_TamperedManifest_ReturnsFalse()
    {
        string originalManifest = """{"version":"1.0","assets":[]}""";
        string tamperedManifest = """{"version":"9.9","assets":[]}""";

        (string armoredPublicKey, string armoredSignature) = GenerateSignedManifest(
            manifest: originalManifest
        );

        bool result = BinaryVerification.VerifyManifestSignature(
            manifestJson: tamperedManifest,
            armoredSignature: armoredSignature,
            armoredPublicKey: armoredPublicKey
        );

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyManifestSignature_WrongPublicKey_ReturnsFalse()
    {
        string manifest = """{"version":"1.0","assets":[]}""";

        (_, string armoredSignature) = GenerateSignedManifest(manifest: manifest);
        (string differentPublicKey, _) = GenerateSignedManifest(manifest: manifest);

        bool result = BinaryVerification.VerifyManifestSignature(
            manifestJson: manifest,
            armoredSignature: armoredSignature,
            armoredPublicKey: differentPublicKey
        );

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyManifestSignature_GarbageSignature_ReturnsFalse()
    {
        string manifest = """{"version":"1.0","assets":[]}""";
        (string armoredPublicKey, _) = GenerateSignedManifest(manifest: manifest);

        bool result = BinaryVerification.VerifyManifestSignature(
            manifestJson: manifest,
            armoredSignature: "this is not a valid pgp signature",
            armoredPublicKey: armoredPublicKey
        );

        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates a fresh RSA PGP key pair, signs <paramref name="manifest"/> with the
    /// private key, and returns the ASCII-armored public key and detached signature.
    /// </summary>
    private static (string ArmoredPublicKey, string ArmoredSignature) GenerateSignedManifest(
        string manifest
    )
    {
        IAsymmetricCipherKeyPairGenerator keyGen = GeneratorUtilities.GetKeyPairGenerator(algorithm: "RSA");
        keyGen.Init(
            parameters: new RsaKeyGenerationParameters(
                publicExponent: Org.BouncyCastle.Math.BigInteger.ValueOf(value: 0x10001),
                random: new(),
                strength: 2048,
                certainty: 12
            )
        );
        AsymmetricCipherKeyPair keyPair = keyGen.GenerateKeyPair();

        PgpKeyPair pgpKeyPair = new(algorithm: PublicKeyAlgorithmTag.RsaGeneral, keyPair: keyPair, time: DateTime.UtcNow);

        // Export armored public key
        string armoredPublicKey;
        using (MemoryStream pubOut = new())
        {
            using ArmoredOutputStream armoredPub = new(outStream: pubOut);
            pgpKeyPair.PublicKey.Encode(outStr: armoredPub);
            armoredPub.Close();
            armoredPublicKey = Encoding.ASCII.GetString(bytes: pubOut.ToArray());
        }

        // Wrap public key in a ring so VerifyManifestSignature can look up by key ID
        PgpPublicKeyRing publicKeyRing = new(encoding: pgpKeyPair.PublicKey.GetEncoded());
        _ = publicKeyRing; // validated by VerifyManifestSignature via key-ID lookup

        // Create detached signature
        PgpSignatureGenerator sigGen = new(
            keyAlgorithm: PublicKeyAlgorithmTag.RsaGeneral,
            hashAlgorithm: HashAlgorithmTag.Sha256
        );
        sigGen.InitSign(sigType: PgpSignature.BinaryDocument, privKey: pgpKeyPair.PrivateKey);

        byte[] data = Encoding.UTF8.GetBytes(s: manifest);
        sigGen.Update(b: data, off: 0, len: data.Length);

        PgpSignature sig = sigGen.Generate();

        string armoredSignature;
        using (MemoryStream sigOut = new())
        {
            using ArmoredOutputStream armoredSig = new(outStream: sigOut);
            sig.Encode(outStream: armoredSig);
            armoredSig.Close();
            armoredSignature = Encoding.ASCII.GetString(bytes: sigOut.ToArray());
        }

        return (armoredPublicKey, armoredSignature);
    }
}
