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

using Newtonsoft.Json;
using NoMercy.Networking.Discovery;

namespace NoMercy.Cli;

internal sealed class CliClient : IDisposable
{
    private readonly IpcClient _client;

    public CliClient(string? pipeNameOrSocketPath = null)
    {
        _client = new(pipeNameOrSocketPath);
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class
    {
        using HttpResponseMessage response = await _client.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            await Console.Error.WriteLineAsync(
                $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
            );
            if (!string.IsNullOrWhiteSpace(body))
                await Console.Error.WriteLineAsync(body);
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonConvert.DeserializeObject<T>(json);
    }

    public async Task<string?> GetRawAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        using HttpResponseMessage response = await _client.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync(
                $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
            );
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<bool> PostAsync(
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
    {
        using HttpResponseMessage response = await _client.PostAsync(
            path,
            content,
            cancellationToken
        );

        if (response.IsSuccessStatusCode)
            return true;

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        await Console.Error.WriteLineAsync(
            $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
        );
        if (!string.IsNullOrWhiteSpace(body))
            await Console.Error.WriteLineAsync(body);

        return false;
    }

    public async Task<T?> PostAsync<T>(
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        using HttpResponseMessage response = await _client.PostAsync(
            path,
            content,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            await Console.Error.WriteLineAsync(
                $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
            );
            if (!string.IsNullOrWhiteSpace(body))
                await Console.Error.WriteLineAsync(body);
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonConvert.DeserializeObject<T>(json);
    }

    public async Task<bool> PutAsync(
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
    {
        using HttpResponseMessage response = await _client.PutAsync(
            path,
            content,
            cancellationToken
        );

        if (response.IsSuccessStatusCode)
            return true;

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        await Console.Error.WriteLineAsync(
            $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
        );
        if (!string.IsNullOrWhiteSpace(body))
            await Console.Error.WriteLineAsync(body);

        return false;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
