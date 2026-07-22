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
/// <see cref="SmbReadStream"/> pulls chunks via <c>ReadFile</c> at an
/// advancing server offset and carries over any bytes the caller's buffer
/// couldn't hold. These tests demand: carry-over correctness across small
/// reads, EOF detection, error propagation, and that Dispose always closes
/// the handle and tears down the session even when CloseFile fails.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class SmbReadStreamTests
{
    private static SmbSession NewSession(Mock<ISMBFileStore> store) =>
        new() { Client = new SMB2Client(), Store = store.Object };

    /// <summary>
    /// Moq's out-parameter matching only supports a fixed value per Setup, so
    /// a stateful sequence (first call returns data, second call returns EOF)
    /// needs Moq's delegate-based <c>Returns</c> overload instead.
    /// </summary>
    private delegate NTStatus InvokeReadFile(
        out byte[] data,
        object handle,
        long offset,
        int maxCount
    );

    [Fact]
    public void Read_returns_bytes_then_zero_at_eof()
    {
        byte[] content = [1, 2, 3, 4, 5];
        Mock<ISMBFileStore> store = new();
        int callIndex = 0;
        store
            .Setup(expression: s =>
                s.ReadFile(
                    out It.Ref<byte[]>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<int>()
                )
            )
            .Returns(
                valueFunction: (InvokeReadFile)(
                    (out byte[] data, object _, long _, int _) =>
                    {
                        if (callIndex++ == 0)
                        {
                            data = content;
                            return NTStatus.STATUS_SUCCESS;
                        }
                        data = [];
                        return NTStatus.STATUS_END_OF_FILE;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");

        byte[] buffer = new byte[content.Length];
        int read = stream.Read(buffer: buffer, offset: 0, count: buffer.Length);
        int readAtEof = stream.Read(buffer: buffer, offset: 0, count: buffer.Length);

        read.Should().Be(expected: content.Length);
        buffer.Should().Equal(elements: content);
        readAtEof
            .Should()
            .Be(expected: 0, because: "a second read past EOF must return 0, not re-issue ReadFile forever");
    }

    [Fact]
    public void Read_with_small_buffer_drains_carry_over_across_multiple_calls()
    {
        byte[] content = [1, 2, 3, 4, 5, 6];
        Mock<ISMBFileStore> store = new();
        int callIndex = 0;
        store
            .Setup(expression: s =>
                s.ReadFile(
                    out It.Ref<byte[]>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<int>()
                )
            )
            .Returns(
                valueFunction: (InvokeReadFile)(
                    (out byte[] data, object _, long _, int _) =>
                    {
                        if (callIndex++ == 0)
                        {
                            data = content;
                            return NTStatus.STATUS_SUCCESS;
                        }
                        data = [];
                        return NTStatus.STATUS_END_OF_FILE;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");

        byte[] small = new byte[2];
        List<byte> collected = [];
        int n;
        while ((n = stream.Read(buffer: small, offset: 0, count: small.Length)) > 0)
            collected.AddRange(collection: small.AsSpan(start: 0, length: n).ToArray());

        collected
            .Should()
            .Equal(expected: content, because: "carry-over must reassemble the full chunk across undersized reads");
        store.Verify(
            expression: s =>
                s.ReadFile(
                    out It.Ref<byte[]>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<int>()
                ),
            times: Times.Exactly(callCount: 2),
            failMessage: "only one native ReadFile should be needed to serve a chunk that fits, plus one to detect EOF"
        );
    }

    [Fact]
    public void Read_with_zero_count_returns_zero_without_calling_ReadFile()
    {
        Mock<ISMBFileStore> store = new();
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");

        stream.Read(buffer: [], offset: 0, count: 0).Should().Be(expected: 0);
        store.Verify(
            expression: s =>
                s.ReadFile(
                    out It.Ref<byte[]>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<int>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public void Read_propagates_non_success_non_eof_status_as_IOException()
    {
        // The stream only distinguishes "error" from "EOF" once it has a
        // non-empty chunk in hand — an empty/null chunk short-circuits to EOF
        // regardless of status (this is what lets a server's EOF-with-error
        // response still terminate the stream cleanly). A non-empty chunk
        // paired with a failure status is the case that must reach
        // SmbStatus.EnsureSuccess and throw.
        Mock<ISMBFileStore> store = new();
        byte[] nonEmptyChunk = [0x01];
        store
            .Setup(expression: s =>
                s.ReadFile(out nonEmptyChunk, It.IsAny<object>(), It.IsAny<long>(), It.IsAny<int>())
            )
            .Returns(value: NTStatus.STATUS_ACCESS_DENIED);
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/secret.bin");

        Action act = () => _ = stream.Read(buffer: new byte[4], offset: 0, count: 4);

        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*STATUS_ACCESS_DENIED*");
    }

    [Fact]
    public async Task ReadAsync_byte_array_overload_delegates_to_Read()
    {
        byte[] content = [7, 8, 9];
        Mock<ISMBFileStore> store = new();
        int callIndex = 0;
        store
            .Setup(expression: s =>
                s.ReadFile(
                    out It.Ref<byte[]>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<int>()
                )
            )
            .Returns(
                valueFunction: (InvokeReadFile)(
                    (out byte[] data, object _, long _, int _) =>
                    {
                        if (callIndex++ == 0)
                        {
                            data = content;
                            return NTStatus.STATUS_SUCCESS;
                        }
                        data = [];
                        return NTStatus.STATUS_END_OF_FILE;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");

        byte[] buffer = new byte[3];
        int read = await stream.ReadAsync(buffer: buffer, offset: 0, count: 3, ct: CancellationToken.None);

        read.Should().Be(expected: 3);
        buffer.Should().Equal(elements: content);
    }

    [Fact]
    public async Task ReadAsync_byte_array_overload_honors_cancellation()
    {
        Mock<ISMBFileStore> store = new();
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = async () => _ = await stream.ReadAsync(buffer: new byte[4], offset: 0, count: 4, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadAsync_memory_overload_delegates_to_Read()
    {
        byte[] content = [3, 2, 1];
        Mock<ISMBFileStore> store = new();
        int callIndex = 0;
        store
            .Setup(expression: s =>
                s.ReadFile(
                    out It.Ref<byte[]>.IsAny,
                    It.IsAny<object>(),
                    It.IsAny<long>(),
                    It.IsAny<int>()
                )
            )
            .Returns(
                valueFunction: (InvokeReadFile)(
                    (out byte[] data, object _, long _, int _) =>
                    {
                        if (callIndex++ == 0)
                        {
                            data = content;
                            return NTStatus.STATUS_SUCCESS;
                        }
                        data = [];
                        return NTStatus.STATUS_END_OF_FILE;
                    }
                )
            );
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");

        byte[] buffer = new byte[3];
        int read = await stream.ReadAsync(buffer: buffer.AsMemory());

        read.Should().Be(expected: 3);
        buffer.Should().Equal(elements: content);
    }

    [Fact]
    public void ReadAsync_memory_overload_honors_cancellation()
    {
        Mock<ISMBFileStore> store = new();
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = async () => _ = await stream.ReadAsync(buffer: new byte[4].AsMemory(), ct: cts.Token);

        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Capability_flags_and_unsupported_members_match_a_forward_only_stream()
    {
        Mock<ISMBFileStore> store = new();
        SmbSession session = NewSession(store: store);
        using SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");

        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeFalse();
        stream.CanWrite.Should().BeFalse();

        ((Action)(() => _ = stream.Length)).Should().Throw<NotSupportedException>();
        ((Action)(() => _ = stream.Position)).Should().Throw<NotSupportedException>();
        ((Action)(() => stream.Position = 0)).Should().Throw<NotSupportedException>();
        ((Action)(() => stream.Seek(offset: 0, origin: SeekOrigin.Begin))).Should().Throw<NotSupportedException>();
        ((Action)(() => stream.SetLength(value: 1))).Should().Throw<NotSupportedException>();
        ((Action)(() => stream.Write(buffer: [1], offset: 0, count: 1))).Should().Throw<NotSupportedException>();
        ((Action)(() => stream.Flush())).Should().NotThrow();
    }

    [Fact]
    public void Dispose_closes_the_handle_and_disposes_the_session()
    {
        Mock<ISMBFileStore> store = new();
        store.Setup(expression: s => s.CloseFile(It.IsAny<object>())).Returns(value: NTStatus.STATUS_SUCCESS);
        store.Setup(expression: s => s.Disconnect()).Returns(value: NTStatus.STATUS_SUCCESS);
        SmbSession session = NewSession(store: store);
        object handle = new();
        SmbReadStream stream = new(session: session, handle: handle, path: "/file.bin");

        stream.Dispose();

        store.Verify(expression: s => s.CloseFile(handle), times: Times.Once);
        store.Verify(
            expression: s => s.Disconnect(),
            times: Times.Once,
            failMessage: "the session must be torn down after the handle closes"
        );
    }

    [Fact]
    public void Dispose_disposes_the_session_even_when_CloseFile_throws()
    {
        Mock<ISMBFileStore> store = new();
        store.Setup(expression: s => s.CloseFile(It.IsAny<object>())).Throws<InvalidOperationException>();
        store.Setup(expression: s => s.Disconnect()).Returns(value: NTStatus.STATUS_SUCCESS);
        SmbSession session = NewSession(store: store);
        SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");

        Action act = () => stream.Dispose();

        act.Should()
            .Throw<InvalidOperationException>(
                because: "CloseFile failures are not swallowed by the stream itself"
            );
        store.Verify(
            expression: s => s.Disconnect(),
            times: Times.Once,
            failMessage: "the session must still be disposed via `finally` even though CloseFile threw"
        );
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        Mock<ISMBFileStore> store = new();
        store.Setup(expression: s => s.CloseFile(It.IsAny<object>())).Returns(value: NTStatus.STATUS_SUCCESS);
        store.Setup(expression: s => s.Disconnect()).Returns(value: NTStatus.STATUS_SUCCESS);
        SmbSession session = NewSession(store: store);
        SmbReadStream stream = new(session: session, handle: new object(), path: "/file.bin");

        stream.Dispose();
        stream.Dispose();

        store.Verify(
            expression: s => s.CloseFile(It.IsAny<object>()),
            times: Times.Once,
            failMessage: "a second Dispose must not re-close the handle"
        );
    }
}
