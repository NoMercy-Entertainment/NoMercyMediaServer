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

using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using NoMercy.NmSystem.Information;

namespace NoMercy.Networking.Discovery;

public sealed class IpcClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SocketsHttpHandler _handler;

    public IpcClient()
        : this(null) { }

    public IpcClient(string? pipeNameOrSocketPath)
    {
        _handler = new()
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string pipeName = pipeNameOrSocketPath ?? Config.ManagementPipeName;
                    NamedPipeClientStream pipe = new(
                        ".",
                        pipeName,
                        PipeDirection.InOut,
                        PipeOptions.Asynchronous
                    );

                    using CancellationTokenSource timeoutCts =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
                    await pipe.ConnectAsync(timeoutCts.Token);
                    return pipe;
                }
                else
                {
                    string socketPath = pipeNameOrSocketPath ?? Config.ManagementSocketPath;
                    Socket socket = new(
                        AddressFamily.Unix,
                        SocketType.Stream,
                        ProtocolType.Unspecified
                    );
                    UnixDomainSocketEndPoint endpoint = new(socketPath);

                    using CancellationTokenSource timeoutCts =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
                    await socket.ConnectAsync(endpoint, timeoutCts.Token);
                    return new NetworkStream(socket, true);
                }
            },
        };

        _httpClient = new(_handler) { BaseAddress = new("http://nomercy-ipc") };
    }

    public Task<HttpResponseMessage> GetAsync(
        string requestUri,
        CancellationToken cancellationToken = default
    )
    {
        return _httpClient.GetAsync(requestUri, cancellationToken);
    }

    public Task<HttpResponseMessage> PostAsync(
        string requestUri,
        HttpContent? content,
        CancellationToken cancellationToken = default
    )
    {
        return _httpClient.PostAsync(requestUri, content, cancellationToken);
    }

    public Task<HttpResponseMessage> PutAsync(
        string requestUri,
        HttpContent? content,
        CancellationToken cancellationToken = default
    )
    {
        return _httpClient.PutAsync(requestUri, content, cancellationToken);
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default
    )
    {
        return _httpClient.SendAsync(request, cancellationToken);
    }

    public Task<HttpResponseMessage> GetStreamAsync(
        string requestUri,
        CancellationToken cancellationToken = default
    )
    {
        HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        return _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}
