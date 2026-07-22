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
        : this(pipeNameOrSocketPath: null) { }

    public IpcClient(string? pipeNameOrSocketPath)
    {
        _handler = new()
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
                {
                    string pipeName = pipeNameOrSocketPath ?? Config.ManagementPipeName;
                    NamedPipeClientStream pipe = new(
                        serverName: ".",
                        pipeName: pipeName,
                        direction: PipeDirection.InOut,
                        options: PipeOptions.Asynchronous
                    );

                    using CancellationTokenSource timeoutCts =
                        CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);
                    timeoutCts.CancelAfter(delay: TimeSpan.FromSeconds(seconds: 3));
                    await pipe.ConnectAsync(cancellationToken: timeoutCts.Token);
                    return pipe;
                }
                else
                {
                    string socketPath = pipeNameOrSocketPath ?? Config.ManagementSocketPath;
                    Socket socket = new(
                        addressFamily: AddressFamily.Unix,
                        socketType: SocketType.Stream,
                        protocolType: ProtocolType.Unspecified
                    );
                    UnixDomainSocketEndPoint endpoint = new(path: socketPath);

                    using CancellationTokenSource timeoutCts =
                        CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);
                    timeoutCts.CancelAfter(delay: TimeSpan.FromSeconds(seconds: 3));
                    await socket.ConnectAsync(remoteEP: endpoint, cancellationToken: timeoutCts.Token);
                    return new NetworkStream(socket: socket, ownsSocket: true);
                }
            },
        };

        _httpClient = new(handler: _handler) { BaseAddress = new(uriString: "http://nomercy-ipc") };
    }

    public Task<HttpResponseMessage> GetAsync(
        string requestUri,
        CancellationToken cancellationToken = default
    )
    {
        return _httpClient.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken);
    }

    public Task<HttpResponseMessage> PostAsync(
        string requestUri,
        HttpContent? content,
        CancellationToken cancellationToken = default
    )
    {
        return _httpClient.PostAsync(requestUri: requestUri, content: content, cancellationToken: cancellationToken);
    }

    public Task<HttpResponseMessage> PutAsync(
        string requestUri,
        HttpContent? content,
        CancellationToken cancellationToken = default
    )
    {
        return _httpClient.PutAsync(requestUri: requestUri, content: content, cancellationToken: cancellationToken);
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default
    )
    {
        return _httpClient.SendAsync(request: request, cancellationToken: cancellationToken);
    }

    public Task<HttpResponseMessage> GetStreamAsync(
        string requestUri,
        CancellationToken cancellationToken = default
    )
    {
        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: requestUri);
        return _httpClient.SendAsync(
            request: request,
            completionOption: HttpCompletionOption.ResponseHeadersRead,
            cancellationToken: cancellationToken
        );
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}
