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

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Unit tests — no Docker needed
// ============================================================================

public class S3DriverConfigParsingTests
{
    private static StorageFactory FactoryWithConfig(string type, string? config)
    {
        Mock<IDriverConfigResolver> driverResolver = new();
        driverResolver.Setup(expression: r => r.Resolve(It.IsAny<Ulid>())).Returns(value: (type, config));

        // Provide dummy credentials so BuildS3 does not reject the config for
        // lacking credentials — these tests verify config parsing, not live S3 access.
        Mock<ICredentialResolver> credResolver = new();
        credResolver
            .Setup(expression: r => r.Resolve(It.IsAny<string>()))
            .Returns(value: ("test-access-key", "test-secret-key"));

        return new(
            driver: new LocalStorageDriver(),
            logger: NullLogger<StorageFactory>.Instance,
            driverConfigResolver: driverResolver.Object,
            credentialResolver: credResolver.Object
        );
    }

    [Fact]
    public void S3DriverConfig_missing_bucket_throws()
    {
        string json = """{"region":"us-east-1"}""";
        StorageFactory factory = FactoryWithConfig(type: "s3", config: json);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*bucket*");
    }

    [Fact]
    public void S3DriverConfig_missing_region_throws()
    {
        string json = """{"bucket":"mybucket"}""";
        StorageFactory factory = FactoryWithConfig(type: "s3", config: json);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*region*");
    }

    [Fact]
    public void R2_without_endpoint_throws()
    {
        string json = """{"bucket":"mybucket","region":"auto"}""";
        StorageFactory factory = FactoryWithConfig(type: "r2", config: json);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*endpoint*");
    }

    [Fact]
    public void Null_configJson_for_s3_throws()
    {
        StorageFactory factory = FactoryWithConfig(type: "s3", config: null);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Malformed_configJson_for_s3_throws_ArgumentException()
    {
        StorageFactory factory = FactoryWithConfig(type: "s3", config: "not-json{{{");
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void S3_with_valid_config_returns_RemoteStorage()
    {
        string json =
            """{"bucket":"test","region":"us-east-1","endpoint":"http://localhost:9000"}""";
        StorageFactory factory = FactoryWithConfig(type: "s3", config: json);
        Ulid driverId = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void R2_with_endpoint_returns_RemoteStorage()
    {
        string json =
            """{"bucket":"test","region":"auto","endpoint":"https://account.r2.cloudflarestorage.com"}""";
        StorageFactory factory = FactoryWithConfig(type: "r2", config: json);
        Ulid driverId = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void For_s3_r2_no_longer_throws_NotSupportedException()
    {
        string json =
            """{"bucket":"test","region":"us-east-1","endpoint":"http://localhost:9000"}""";
        StorageFactory factory = FactoryWithConfig(type: "s3", config: json);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().NotThrow<NotSupportedException>();
    }
}

// ============================================================================
// Integration tests — run against the shared all-in-one storage container
// (MinIO S3). The container is started once for the whole assembly by the
// StorageBackends collection fixture and torn down after the last test.
// ============================================================================

[Collection(name: "StorageBackends")]
public class S3StorageDriverIntegrationTests(StorageBackendsFixture fix)
{
    private string SkipReason => fix.StartupError ?? "storage container not available";

    [SkippableFact]
    public async Task RoundTrip_write_read_delete()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string path = $"roundtrip/{Ulid.NewUlid()}.txt";
        byte[] data = "hello s3"u8.ToArray();

        // Write
        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
        {
            await w.WriteAsync(buffer: data);
        }

        // Read
        await using Stream r = backend.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);
        ms.ToArray().Should().Equal(elements: data);

        // Delete
        backend.DeleteFile(path: path);
        backend.FileExists(path: path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Multipart_upload_large_file()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string path = $"large/{Ulid.NewUlid()}.bin";

        // 6 MB — exceeds the 5 MB multipart threshold
        byte[] data = new byte[6 * 1024 * 1024];
        new Random(Seed: 42).NextBytes(buffer: data);

        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
        {
            await w.WriteAsync(buffer: data);
        }

        long size = backend.GetFileSize(path: path);
        size.Should().Be(expected: data.Length);

        backend.DeleteFile(path: path);
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_prefix()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver(prefix: "enum-test");
        string prefix = $"dir-{Ulid.NewUlid()}";

        // Write two files under the prefix
        string fileA = $"{prefix}/a.txt";
        string fileB = $"{prefix}/b.txt";
        byte[] bytes = "x"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path: fileA, overwrite: true))
            await w.WriteAsync(buffer: bytes);
        await using (Stream w2 = backend.OpenWrite(path: fileB, overwrite: true))
            await w2.WriteAsync(buffer: bytes);

        IEnumerable<string> entries = backend.EnumerateFileSystemEntries(
            directory: prefix,
            searchPattern: "*.txt",
            option: SearchOption.AllDirectories
        );

        entries.Should().HaveCount(expected: 2);

        backend.DeleteDirectory(path: prefix, recursive: true);
    }

    [SkippableFact]
    public async Task MoveFile_renames_key()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string src = $"move/{Ulid.NewUlid()}-src.txt";
        string dst = $"move/{Ulid.NewUlid()}-dst.txt";
        byte[] data = "move me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path: src, overwrite: true))
            await w.WriteAsync(buffer: data);

        backend.MoveFile(source: src, destination: dst);

        backend.FileExists(path: src).Should().BeFalse();
        backend.FileExists(path: dst).Should().BeTrue();

        backend.DeleteFile(path: dst);
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_key()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string src = $"copy/{Ulid.NewUlid()}-src.txt";
        string dst = $"copy/{Ulid.NewUlid()}-dst.txt";
        byte[] data = "copy me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path: src, overwrite: true))
            await w.WriteAsync(buffer: data);

        backend.CopyFile(source: src, destination: dst, overwrite: true);

        backend.FileExists(path: src).Should().BeTrue();
        backend.FileExists(path: dst).Should().BeTrue();

        backend.DeleteFile(path: src);
        backend.DeleteFile(path: dst);
    }

    [SkippableFact]
    public async Task OpenRead_large_file_streams_correctly()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string path = $"stream/{Ulid.NewUlid()}.bin";
        byte[] data = new byte[2 * 1024 * 1024];
        new Random(Seed: 7).NextBytes(buffer: data);

        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);

        await using Stream r = backend.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);

        ms.ToArray().Should().HaveCount(expected: data.Length);
        backend.DeleteFile(path: path);
    }

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_rejects_existing()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string path = $"nooverwrite/{Ulid.NewUlid()}.txt";
        byte[] data = "original"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);

        Action act = () => backend.OpenWrite(path: path, overwrite: false);
        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*overwrite*");

        backend.DeleteFile(path: path);
    }
}

// Required by xUnit to share the fixture across the collection

// ============================================================================
// Unit tests — EnumerateFileSystemEntries contract (no Docker)
// ============================================================================

public class S3EnumerateContractTests
{
    private static S3StorageDriver BuildDriver(
        Mock<IAmazonS3> mockClient,
        string bucket,
        string prefix
    )
    {
        return new(client: mockClient.Object, bucket: bucket, prefix: prefix);
    }

    [Fact]
    public void EnumerateFileSystemEntries_strips_prefix_from_file_keys()
    {
        Mock<IAmazonS3> mock = new();

        ListObjectsV2Response listResponse = new()
        {
            S3Objects = [new() { Key = "media/folder/file.mp3" }],
            CommonPrefixes = ["media/folder/"],
            IsTruncated = false,
        };

        mock.Setup(expression: c =>
                c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.Delimiter == "/"), default)
            )
            .ReturnsAsync(value: listResponse);

        S3StorageDriver driver = BuildDriver(mockClient: mock, bucket: "test-bucket", prefix: "media");

        List<string> entries = driver
            .EnumerateFileSystemEntries(directory: string.Empty, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .ToList();

        entries.Should().Contain(expected: "folder/file.mp3");
        entries.Should().Contain(expected: "folder");
        entries.Should().NotContain(predicate: e => e.StartsWith("media/"));
    }

    [Fact]
    public void EnumerateFileSystemEntries_DirectoryExists_roundtrip()
    {
        Mock<IAmazonS3> mock = new();

        // Enumerate call
        ListObjectsV2Response listResponse = new()
        {
            S3Objects = [],
            CommonPrefixes = ["media/folder/"],
            IsTruncated = false,
        };

        mock.Setup(expression: c =>
                c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.Delimiter == "/"), default)
            )
            .ReturnsAsync(value: listResponse);

        // DirectoryExists call — expects prefix "media/folder/" (ToKey("folder") = "media/folder/")
        ListObjectsV2Response existsResponse = new()
        {
            S3Objects = [new() { Key = "media/folder/track.mp3" }],
            CommonPrefixes = [],
            IsTruncated = false,
        };

        mock.Setup(expression: c =>
                c.ListObjectsV2Async(
                    It.Is<ListObjectsV2Request>(r => r.Prefix == "media/folder/" && r.MaxKeys == 1),
                    default
                )
            )
            .ReturnsAsync(value: existsResponse);

        S3StorageDriver driver = BuildDriver(mockClient: mock, bucket: "test-bucket", prefix: "media");

        List<string> entries = driver
            .EnumerateFileSystemEntries(directory: string.Empty, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .ToList();

        string dirEntry = entries.Single(predicate: e => e == "folder");
        bool exists = driver.DirectoryExists(path: dirEntry);

        exists
            .Should()
            .BeTrue(because: "round-trip from EnumerateFileSystemEntries to DirectoryExists must work");
    }
}
