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
using System.Text;

namespace NoMercy.Tests.Cli.Support;

/// <summary>
/// A minimal stand-in for the management IPC server that <c>NoMercy.Cli</c> talks
/// to. This is the real transport <see cref="NoMercy.Networking.Discovery.IpcClient"/>
/// uses (see its <c>ConnectCallback</c>) — every test that uses this class is
/// exercising the genuine IPC + raw-HTTP wire format, not a mock of
/// <c>CliClient</c>/<c>IpcClient</c> themselves. Each instance binds a unique,
/// GUID-suffixed name so parallel or repeated test runs never collide with each
/// other or with a real, running management server.
/// </summary>
/// <remarks>
/// The transport has to be chosen the same way <c>IpcClient</c> chooses it, per
/// platform: a named pipe on Windows, a Unix domain socket everywhere else. A
/// <see cref="NamedPipeServerStream"/> on Linux is served from
/// <c>/tmp/CoreFxPipe_&lt;name&gt;</c>, while <c>IpcClient</c>'s non-Windows branch
/// connects to the string it is given as a literal socket path — so a pipe-only
/// fixture can never be reached on the Linux CI runner, and every test here fails
/// with "Cannot assign requested address" as the client falls back to resolving
/// the base address over TCP.
/// </remarks>
internal sealed class FakeManagementPipeServer
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// What the client should be handed: a bare pipe name on Windows, an absolute
    /// socket path on Unix. Both are what <c>IpcClient</c> expects for that platform.
    /// </summary>
    public string PipeName { get; } =
        IsWindows
            ? $"nomercy-test-{Guid.NewGuid():N}"
            : Path.Combine(Path.GetTempPath(), $"nomercy-test-{Guid.NewGuid():N}.sock");

    /// <summary>
    /// Waits for a single client connection, reads the request line into a
    /// string, then hands the raw stream to <paramref name="respond"/> so
    /// the caller can write back whatever raw HTTP bytes the scenario needs.
    /// </summary>
    public async Task<string> RunOnceAsync(
        Func<Stream, Task> respond,
        CancellationToken ct = default
    )
    {
        return IsWindows
            ? await RunOnceOverNamedPipeAsync(respond, ct)
            : await RunOnceOverUnixSocketAsync(respond, ct);
    }

    private async Task<string> RunOnceOverNamedPipeAsync(
        Func<Stream, Task> respond,
        CancellationToken ct
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

    private async Task<string> RunOnceOverUnixSocketAsync(
        Func<Stream, Task> respond,
        CancellationToken ct
    )
    {
        // A bound socket file left behind by a previous step would make Bind fail
        // with "Address already in use" — RunSequenceAsync rebinds per connection.
        File.Delete(PipeName);

        using Socket listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(PipeName));
        listener.Listen(1);

        try
        {
            using Socket connection = await listener.AcceptAsync(ct);
            await using NetworkStream stream = new(connection, false);

            string request = await ReadRequestAsync(stream, ct);
            await respond(stream);

            return request;
        }
        finally
        {
            File.Delete(PipeName);
        }
    }

    private static async Task<string> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        int read = await stream.ReadAsync(buffer, ct);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    /// <summary>
    /// Handles a fixed number of connections on this pipe name, one at a time
    /// and in order. A real HTTP/1.1 client tears the connection down after any
    /// "Connection: close" response (every scenario here sends one) and opens
    /// a fresh connection for its next request — so a multi-step command like
    /// <c>update</c> (download, then stop, then poll) needs a fresh
    /// <see cref="NamedPipeServerStream"/> instance per step, exactly like a
    /// real server would create.
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
    /// given headers, a blank line, then the body) and leaves the connection
    /// open for the caller to close explicitly — matching how a real streaming
    /// (SSE) response is framed: no Content-Length, "Connection: close" lets the
    /// client detect end-of-body when the stream is closed.
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
