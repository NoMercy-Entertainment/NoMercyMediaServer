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
using System.Text;

namespace NoMercy.Tests.Launcher.Support;

/// <summary>
/// A minimal stand-in for the management IPC server that <c>NoMercy.Launcher</c>'s
/// <c>ServerConnection</c> talks to over a Windows named pipe. This is the real
/// transport <see cref="NoMercy.Networking.Discovery.IpcClient"/> uses on Windows
/// (see its <c>ConnectCallback</c>) — every test that uses this class is
/// exercising the genuine named-pipe + raw-HTTP wire format, not a mock of
/// <c>ServerConnection</c>/<c>IpcClient</c> themselves. Each instance binds a
/// unique, GUID-suffixed pipe name so parallel or repeated test runs never
/// collide with each other or with a real, running management server.
/// </summary>
internal sealed class FakeManagementPipeServer
{
    public string PipeName { get; } = $"nomercy-test-{Guid.NewGuid():N}";

    /// <summary>
    /// Waits for a single client connection, reads the request line into a
    /// string, then hands the raw pipe stream to <paramref name="respond"/> so
    /// the caller can write back whatever raw HTTP bytes the scenario needs.
    /// </summary>
    public async Task<string> RunOnceAsync(
        Func<Stream, Task> respond,
        CancellationToken ct = default
    )
    {
        await using NamedPipeServerStream server = new(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );

        await server.WaitForConnectionAsync(ct);

        string request = await ReadRequestAsync(server, ct);
        await respond(server);

        return request;
    }

    private static async Task<string> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        int read = await stream.ReadAsync(buffer, ct);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    /// <summary>
    /// Handles a fixed number of connections on this pipe name, one at a time
    /// and in order — a multi-step flow (connect, then a follow-up call) needs
    /// a fresh <see cref="NamedPipeServerStream"/> instance per step, exactly
    /// like a real server would create.
    /// </summary>
    public async Task<List<string>> RunSequenceAsync(params Func<Stream, Task>[] responders)
    {
        List<string> requests = [];

        foreach (Func<Stream, Task> responder in responders)
        {
            requests.Add(await RunOnceAsync(responder));
        }

        return requests;
    }

    /// <summary>
    /// Writes a complete, close-delimited HTTP/1.1 response (status line, the
    /// given headers, a blank line, then the body) — matching how a real
    /// streaming (SSE) response is framed: no Content-Length, "Connection:
    /// close" lets the client detect end-of-body when the stream is closed.
    /// </summary>
    public static async Task WriteResponseAsync(
        Stream stream,
        int statusCode,
        string reasonPhrase,
        string body,
        string contentType = "application/json",
        CancellationToken ct = default
    )
    {
        StringBuilder sb = new();
        sb.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(reasonPhrase).Append("\r\n");
        sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append("\r\n");
        sb.Append(body);

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await stream.WriteAsync(bytes, ct);
    }
}
