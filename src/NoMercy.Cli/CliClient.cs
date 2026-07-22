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

internal sealed class CliClient : ICliClient
{
    private readonly IpcClient _client;

    public CliClient(string? pipeNameOrSocketPath = null)
    {
        _client = new(pipeNameOrSocketPath: pipeNameOrSocketPath);
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class
    {
        using HttpResponseMessage response = await _client.GetAsync(requestUri: path, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            await Console.Error.WriteLineAsync(
                value: $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
            );
            if (!string.IsNullOrWhiteSpace(value: body))
                await Console.Error.WriteLineAsync(value: body);
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

        return JsonConvert.DeserializeObject<T>(value: json);
    }

    public async Task<string?> GetRawAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        using HttpResponseMessage response = await _client.GetAsync(requestUri: path, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync(
                value: $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
            );
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> PostAsync(
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
    {
        using HttpResponseMessage response = await _client.PostAsync(
            requestUri: path,
            content: content,
            cancellationToken: cancellationToken
        );

        if (response.IsSuccessStatusCode)
            return true;

        string body = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        await Console.Error.WriteLineAsync(
            value: $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
        );
        if (!string.IsNullOrWhiteSpace(value: body))
            await Console.Error.WriteLineAsync(value: body);

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
            requestUri: path,
            content: content,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            await Console.Error.WriteLineAsync(
                value: $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
            );
            if (!string.IsNullOrWhiteSpace(value: body))
                await Console.Error.WriteLineAsync(value: body);
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

        return JsonConvert.DeserializeObject<T>(value: json);
    }

    public async Task<bool> PutAsync(
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
    {
        using HttpResponseMessage response = await _client.PutAsync(
            requestUri: path,
            content: content,
            cancellationToken: cancellationToken
        );

        if (response.IsSuccessStatusCode)
            return true;

        string body = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        await Console.Error.WriteLineAsync(
            value: $"Error: {(int)response.StatusCode} {response.ReasonPhrase}"
        );
        if (!string.IsNullOrWhiteSpace(value: body))
            await Console.Error.WriteLineAsync(value: body);

        return false;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
