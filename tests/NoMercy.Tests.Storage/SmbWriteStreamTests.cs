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

using NoMercy.Storage.Drivers.Smb;
using SMBLibrary;
using SMBLibrary.Client;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="SmbWriteStream"/> streams incoming bytes to the share via
/// <c>WriteFile</c> at an advancing offset, chunked to the negotiated
/// <c>MaxWriteSize</c> ceiling so a multi-GB upload never buffers in memory.
/// These tests demand: chunking splits oversized writes correctly, a short
/// native write is retried from where it left off (never silently dropped),
/// errors propagate, and Dispose always closes the handle and disposes the
/// session even when CloseFile fails.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class SmbWriteStreamTests
{
    private static SmbSession NewSession(Mock<ISMBFileStore> store) =>
        new() { Client = new SMB2Client(), Store = store.Object };

    private delegate NTStatus InvokeWriteFile(
        out int written,
        object handle,
        long offset,
        byte[] data
    );

    [Fact]
    public void Write_sends_the_full_payload_in_one_native_call_when_it_fits_the_chunk_size()
    {
        Mock<ISMBFileStore> store = new();
        List<byte[]> capturedChunks = [];
        store
            .Setup(expression: s =>
                s.WriteFile(
                    out It.Ref<int>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<byte[]>()
                )
            )
            .Returns(
                valueFunction: (InvokeWriteFile)(
                    (out int written, object _, long _, byte[] data) =>
                    {
                        capturedChunks.Add(item: data);
                        written = data.Length;
                        return NTStatus.STATUS_SUCCESS;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin", chunkSize: 1024);
        byte[] payload = [1, 2, 3, 4, 5];

        stream.Write(buffer: payload, offset: 0, count: payload.Length);

        capturedChunks.Should().HaveCount(expected: 1);
        capturedChunks[index: 0].Should().Equal(elements: payload);
    }

    [Fact]
    public void Write_splits_a_payload_larger_than_the_chunk_size_into_multiple_native_calls()
    {
        Mock<ISMBFileStore> store = new();
        List<byte[]> capturedChunks = [];
        store
            .Setup(expression: s =>
                s.WriteFile(
                    out It.Ref<int>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<byte[]>()
                )
            )
            .Returns(
                valueFunction: (InvokeWriteFile)(
                    (out int written, object _, long _, byte[] data) =>
                    {
                        capturedChunks.Add(item: data);
                        written = data.Length;
                        return NTStatus.STATUS_SUCCESS;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin", chunkSize: 3);
        byte[] payload = [1, 2, 3, 4, 5, 6, 7];

        stream.Write(buffer: payload, offset: 0, count: payload.Length);

        capturedChunks.Should().HaveCount(expected: 3, because: "7 bytes at chunkSize=3 must split into 3+3+1");
        capturedChunks.SelectMany(selector: c => c).Should().Equal(elements: payload);
    }

    [Fact]
    public void Write_resumes_from_a_short_native_write_instead_of_dropping_bytes()
    {
        // A server that only accepts part of a chunk in one WriteFile call
        // must not lose the remainder — the stream must keep calling
        // WriteFile at the advanced offset until the whole chunk is sent.
        Mock<ISMBFileStore> store = new();
        List<(long offset, byte[] data)> calls = [];
        store
            .Setup(expression: s =>
                s.WriteFile(
                    out It.Ref<int>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<byte[]>()
                )
            )
            .Returns(
                valueFunction: (InvokeWriteFile)(
                    (out int written, object _, long offset, byte[] data) =>
                    {
                        calls.Add(item: (offset, data));
                        // Server only accepts the first 2 bytes of whatever it's handed.
                        written = Math.Min(val1: 2, val2: data.Length);
                        return NTStatus.STATUS_SUCCESS;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin", chunkSize: 100);
        byte[] payload = [1, 2, 3, 4, 5];

        stream.Write(buffer: payload, offset: 0, count: payload.Length);

        calls
            .Should()
            .HaveCount(expected: 3, because: "5 bytes at 2-bytes-accepted-per-call must take 3 calls (2+2+1)");
        calls[index: 0].offset.Should().Be(expected: 0);
        calls[index: 1]
            .offset.Should()
            .Be(expected: 2, because: "the offset must advance by exactly what the server actually wrote");
        calls[index: 2].offset.Should().Be(expected: 4);
    }

    [Fact]
    public void Write_span_overload_delegates_to_the_array_overload()
    {
        Mock<ISMBFileStore> store = new();
        byte[]? captured = null;
        store
            .Setup(expression: s =>
                s.WriteFile(
                    out It.Ref<int>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<byte[]>()
                )
            )
            .Returns(
                valueFunction: (InvokeWriteFile)(
                    (out int written, object _, long _, byte[] data) =>
                    {
                        captured = data;
                        written = data.Length;
                        return NTStatus.STATUS_SUCCESS;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin");

        stream.Write(buffer: new ReadOnlySpan<byte>(array: [9, 8, 7]));

        captured.Should().Equal(elements: [9, 8, 7]);
    }

    [Fact]
    public async Task WriteAsync_byte_array_overload_writes_and_honors_cancellation()
    {
        Mock<ISMBFileStore> store = new();
        store
            .Setup(expression: s =>
                s.WriteFile(
                    out It.Ref<int>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<byte[]>()
                )
            )
            .Returns(
                valueFunction: (InvokeWriteFile)(
                    (out int written, object _, long _, byte[] data) =>
                    {
                        written = data.Length;
                        return NTStatus.STATUS_SUCCESS;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin");

        await stream.WriteAsync(buffer: [1, 2, 3], offset: 0, count: 3, ct: CancellationToken.None);
        stream.Position.Should().Be(expected: 3);

        using CancellationTokenSource cts = new();
        cts.Cancel();
        Func<Task> act = () => stream.WriteAsync(buffer: [1], offset: 0, count: 1, ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteAsync_memory_overload_writes_and_honors_cancellation()
    {
        Mock<ISMBFileStore> store = new();
        store
            .Setup(expression: s =>
                s.WriteFile(
                    out It.Ref<int>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<byte[]>()
                )
            )
            .Returns(
                valueFunction: (InvokeWriteFile)(
                    (out int written, object _, long _, byte[] data) =>
                    {
                        written = data.Length;
                        return NTStatus.STATUS_SUCCESS;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin");

        await stream.WriteAsync(buffer: new byte[] { 4, 5 }.AsMemory());
        stream.Position.Should().Be(expected: 2);

        using CancellationTokenSource cts = new();
        cts.Cancel();
        Func<Task> act = async () => await stream.WriteAsync(buffer: new byte[1].AsMemory(), ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Write_propagates_native_error_as_IOException()
    {
        Mock<ISMBFileStore> store = new();
        store
            .Setup(expression: s =>
                s.WriteFile(
                    out It.Ref<int>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<byte[]>()
                )
            )
            .Returns(
                valueFunction: (InvokeWriteFile)(
                    (out int written, object _, long _, byte[] _) =>
                    {
                        written = 0;
                        return NTStatus.STATUS_DISK_FULL;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin");

        Action act = () => stream.Write(buffer: [1, 2, 3], offset: 0, count: 3);

        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*STATUS_DISK_FULL*");
    }

    [Fact]
    public void Capability_flags_and_unsupported_members_match_a_forward_only_write_stream()
    {
        Mock<ISMBFileStore> store = new();
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin");

        stream.CanRead.Should().BeFalse();
        stream.CanSeek.Should().BeFalse();
        stream.CanWrite.Should().BeTrue();

        ((Action)(() => stream.Position = 0)).Should().Throw<NotSupportedException>();
        ((Action)(() => stream.Seek(offset: 0, origin: SeekOrigin.Begin))).Should().Throw<NotSupportedException>();
        ((Action)(() => stream.SetLength(value: 1))).Should().Throw<NotSupportedException>();
        ((Action)(() => _ = stream.Read(buffer: new byte[1], offset: 0, count: 1)))
            .Should()
            .Throw<NotSupportedException>();
        ((Action)(() => stream.Flush())).Should().NotThrow();
    }

    [Fact]
    public void Length_tracks_the_number_of_bytes_written_so_far()
    {
        Mock<ISMBFileStore> store = new();
        store
            .Setup(expression: s =>
                s.WriteFile(
                    out It.Ref<int>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<byte[]>()
                )
            )
            .Returns(
                valueFunction: (InvokeWriteFile)(
                    (out int written, object _, long _, byte[] data) =>
                    {
                        written = data.Length;
                        return NTStatus.STATUS_SUCCESS;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin");

        stream.Write(buffer: [1, 2, 3], offset: 0, count: 3);

        stream.Length.Should().Be(expected: 3);
        stream.Position.Should().Be(expected: 3);
    }

    [Fact]
    public void Dispose_closes_the_handle_and_disposes_the_session()
    {
        Mock<ISMBFileStore> store = new();
        store.Setup(expression: s => s.CloseFile(It.IsAny<object>())).Returns(value: NTStatus.STATUS_SUCCESS);
        store.Setup(expression: s => s.Disconnect()).Returns(value: NTStatus.STATUS_SUCCESS);
        SmbSession session = NewSession(store: store);
        object handle = new();
        SmbWriteStream stream = new(session: session, handle: handle, path: "/out.bin");

        stream.Dispose();

        store.Verify(expression: s => s.CloseFile(handle), times: Times.Once);
        store.Verify(expression: s => s.Disconnect(), times: Times.Once);
    }

    [Fact]
    public void Dispose_disposes_the_session_even_when_CloseFile_throws()
    {
        Mock<ISMBFileStore> store = new();
        store.Setup(expression: s => s.CloseFile(It.IsAny<object>())).Throws<InvalidOperationException>();
        store.Setup(expression: s => s.Disconnect()).Returns(value: NTStatus.STATUS_SUCCESS);
        SmbSession session = NewSession(store: store);
        SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin");

        Action act = () => stream.Dispose();

        act.Should().Throw<InvalidOperationException>();
        store.Verify(
            expression: s => s.Disconnect(),
            times: Times.Once,
            failMessage: "session teardown must still run via `finally`"
        );
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        Mock<ISMBFileStore> store = new();
        store.Setup(expression: s => s.CloseFile(It.IsAny<object>())).Returns(value: NTStatus.STATUS_SUCCESS);
        store.Setup(expression: s => s.Disconnect()).Returns(value: NTStatus.STATUS_SUCCESS);
        SmbSession session = NewSession(store: store);
        SmbWriteStream stream = new(session: session, handle: new object(), path: "/out.bin");

        stream.Dispose();
        stream.Dispose();

        store.Verify(expression: s => s.CloseFile(It.IsAny<object>()), times: Times.Once);
    }
}
