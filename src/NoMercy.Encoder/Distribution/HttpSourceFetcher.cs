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
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Downloads task source files from the coordinator over HTTP when the
/// worker can't see the original path locally. The coordinator's
/// /worker/source endpoint signs the download URL via HMAC over
/// (path, timestamp) so a worker that doesn't have the signing key
/// can't guess URLs.
///
/// Cached downloads live under <see cref="EncoderOptions.LiveTranscodeCachePath"/>/
/// remote-sources. Each task's fetched file gets a deterministic name
/// derived from the original path hash so retries for the same task
/// reuse the file instead of re-downloading.
/// </summary>
public class HttpSourceFetcher(
    IHttpClientFactory httpClientFactory,
    EncoderOptions options,
    ILogger<HttpSourceFetcher> logger,
    IStorage storage
) : ISourceFetcher
{
    public async Task<string> EnsureLocalAsync(EncodeTask task, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value: task.InputPath))
            return string.Empty;

        // Fast path: source is visible locally (shared NAS / SMB mount).
        if (storage.Exists(path: task.InputPath))
            return task.InputPath;

        // Coordinator URL required for fetch — when it's not set, this
        // worker is standalone and there's no remote source to fetch from.
        if (string.IsNullOrWhiteSpace(value: options.CoordinatorUrl))
        {
            logger.LogWarning(
                message: "Task {TaskId} source {Path} is missing locally and no CoordinatorUrl configured", args: [task.TaskId, task.InputPath]
            );
            return task.InputPath;
        }

        string cachedPath = ResolveCachePath(task: task);

        // Idempotency: if the file already exists from a previous attempt
        // and isn't empty, reuse it. Full hash-match check is overkill;
        // HMAC on the coordinator's response covers integrity.
        if (storage.Exists(path: cachedPath) && storage.SizeOrZero(path: cachedPath) > 0)
        {
            logger.LogInformation(
                message: "Task {TaskId} reusing cached source at {Path}", args: [task.TaskId, cachedPath]
            );
            return cachedPath;
        }

        storage.CreateDirectory(path: Path.GetDirectoryName(path: cachedPath)!);

        HttpClient http = httpClientFactory.CreateClient(name: "worker-source-fetch");
        http.BaseAddress = new(uriString: options.CoordinatorUrl);
        http.Timeout = TimeSpan.FromHours(hours: 1); // Multi-GB downloads over WAN.

        string signedQuery = BuildSignedQuery(path: task.InputPath!);
        string requestPath = $"api/v1/worker/source?{signedQuery}";
        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: requestPath);

        if (options.IsDistributedEncodingEnabled)
        {
            HmacSigner hmacSigner = new(secret: options.DistributedEncodingSigningKey!);
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string signature = hmacSigner.Sign(method: "GET", path: "/" + "api/v1/worker/source", timestamp: timestamp, body: []);
            request.Headers.Add(name: "X-NoMercy-Timestamp", value: timestamp.ToString());
            request.Headers.Add(name: "X-NoMercy-Signature", value: signature);
        }

        logger.LogInformation(message: "Fetching source for task {TaskId} from coordinator", args: task.TaskId);

        using HttpResponseMessage response = await http.SendAsync(
                request: request,
                completionOption: HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: ct
            )
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
            logger.LogError(
                message: "Coordinator returned {Status} fetching source for task {TaskId}: {Body}", args: [(int)response.StatusCode, task.TaskId, body.Length > 500 ? body[..500] : body]
            );
            throw new InvalidOperationException(
                message: $"Source fetch failed: HTTP {(int)response.StatusCode}"
            );
        }

        await using (
            Stream source = await response.Content.ReadAsStreamAsync(cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false)
        )
        await using (Stream target = await storage.OpenWriteAsync(path: cachedPath, overwrite: true, ct: ct))
        {
            await source.CopyToAsync(destination: target, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
        }

        logger.LogInformation(
            message: "Task {TaskId} source fetched to {Path} ({Bytes} bytes)", args: [task.TaskId, cachedPath, storage.SizeOrZero(path: cachedPath)]
        );

        return cachedPath;
    }

    public Task ReleaseAsync(EncodeTask task)
    {
        // Cached downloads are keyed by task — drop the specific file.
        // Keep the parent directory; concurrent tasks may still have
        // cached sources there.
        try
        {
            string cachedPath = ResolveCachePath(task: task);
            storage.Delete(path: cachedPath);
            logger.LogDebug(message: "Released cached source for task {TaskId}", args: task.TaskId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Failed to release cached source for task {TaskId} — will persist until next cache cleanup",
                args: task.TaskId
            );
        }
        return Task.CompletedTask;
    }

    private string ResolveCachePath(EncodeTask task)
    {
        string cacheRoot = Path.Combine(path1: options.ResolvedLiveTranscodeCachePath, path2: "remote-sources");

        // Deterministic filename per task — task.TaskId is unique per job,
        // suffixed with the source file's extension so ffprobe/ffmpeg
        // auto-detect the container from the extension.
        string ext = Path.GetExtension(path: task.InputPath ?? string.Empty);
        if (string.IsNullOrEmpty(value: ext))
            ext = ".src";

        return Path.Combine(path1: cacheRoot, path2: $"{task.TaskId}{ext}");
    }

    private string BuildSignedQuery(string path)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] key = options.GetDistributedEncodingSigningKey();
        string signatureInput = $"{path}|{timestamp}";
        using HMACSHA256 hmac = new(key: key);
        string signature = Convert.ToBase64String(
            inArray: hmac.ComputeHash(buffer: Encoding.UTF8.GetBytes(s: signatureInput))
        );

        // URL-encode the path and signature; timestamp is a simple int.
        string encodedPath = Uri.EscapeDataString(stringToEscape: path);
        string encodedSig = Uri.EscapeDataString(stringToEscape: signature);
        return $"path={encodedPath}&ts={timestamp}&sig={encodedSig}";
    }
}
