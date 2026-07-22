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

using NoMercy.Encoder.Subtitles;
using NoMercy.Setup.Dto;
using NoMercy.Storage;

namespace NoMercy.Setup.Server;

/// <summary>
/// Implements <see cref="ITesseractModelDownloader"/> against the same signed-release
/// machinery (<see cref="Binaries.GetLatestReleaseInfo"/>,
/// <see cref="Binaries.GetOrFetchManifestAsync"/>, <see cref="BinaryVerification"/>) that
/// the startup binary provisioning pipeline uses — so an on-demand OCR model pull is held
/// to the exact signature + SHA-256 bar as every other NoMercy-owned download.
/// </summary>
/// <remarks>
/// Deliberately stricter than <see cref="Binaries.DownloadWithVerificationAsync"/>'s
/// digest-fallback policy: this path is triggered on-demand for an asset the server has
/// never installed before (no "known-good binary already on disk" to fall back on), and
/// nomercy-tesseract's releases publish a signed manifest today, so a missing or
/// non-verifying signature is treated as a hard failure rather than a soft downgrade to
/// GitHub's asset digest. No unverified raw-URL fallback is ever attempted.
/// </remarks>
public class TesseractModelDownloader(
    IStorageDriver driver,
    IStorage storage,
    HttpClient httpClient
) : ITesseractModelDownloader
{
    private const string TesseractReleaseApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-tesseract/releases/latest";

    // Reuses Binaries' release-info cache (with its GitHub rate-limit backoff and
    // last-known-good disk cache) and its manifest cache — both keyed per apiUrl — so
    // repeated language downloads on this singleton never re-fetch or re-verify the same
    // release manifest.
    private readonly Binaries _binaries = new(driver: driver, storage: storage, httpClient: httpClient);

    public async Task<Stream> DownloadVerifiedAsync(string language, CancellationToken ct)
    {
        string assetName = $"{language}.traineddata";

        GithubReleaseResponse releaseInfo = await _binaries.GetLatestReleaseInfo(
            apiUrl: TesseractReleaseApiUrl
        );
        if (releaseInfo.Assets.Length == 0)
        {
            throw new InvalidOperationException(
                message: $"Could not reach the nomercy-tesseract release — no signed model available for '{language}'."
            );
        }

        (ReleaseManifest? manifest, bool sigVerified, bool sigPresent) =
            await _binaries.GetOrFetchManifestAsync(apiUrl: TesseractReleaseApiUrl, releaseInfo: releaseInfo);

        if (manifest is null)
        {
            throw new InvalidOperationException(
                message: $"No signed release manifest is available for nomercy-tesseract — refusing to "
                         + $"install an unverified model for '{language}'."
            );
        }

        if (!sigPresent || !sigVerified)
        {
            throw new InvalidOperationException(
                message: $"nomercy-tesseract release manifest signature could not be verified — refusing "
                         + $"to install an unverified model for '{language}'."
            );
        }

        ManifestAsset? manifestAsset = manifest.Assets.FirstOrDefault(predicate: a =>
            a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
        );
        if (manifestAsset is null)
        {
            throw new InvalidOperationException(
                message: $"No signed manifest entry for '{assetName}' in the nomercy-tesseract release."
            );
        }

        Asset? releaseAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
            a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
        );
        if (releaseAsset is null)
        {
            throw new InvalidOperationException(
                message: $"No release asset for '{assetName}' in the nomercy-tesseract release."
            );
        }

        using HttpResponseMessage response = await httpClient.GetAsync(
            requestUri: releaseAsset.BrowserDownloadUrl,
            completionOption: HttpCompletionOption.ResponseHeadersRead,
            cancellationToken: ct
        );
        response.EnsureSuccessStatusCode();

        byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken: ct);
        MemoryStream payloadStream = new(buffer: payload);

        bool hashOk = await BinaryVerification.VerifyStreamSha256Async(
            stream: payloadStream,
            expectedHex: manifestAsset.Sha256,
            ct: ct
        );
        if (!hashOk)
        {
            await payloadStream.DisposeAsync();
            throw new InvalidDataException(
                message: $"SHA-256 mismatch for '{assetName}': the downloaded model does not match the "
                         + "signed nomercy-tesseract manifest."
            );
        }

        payloadStream.Position = 0;
        return payloadStream;
    }
}
