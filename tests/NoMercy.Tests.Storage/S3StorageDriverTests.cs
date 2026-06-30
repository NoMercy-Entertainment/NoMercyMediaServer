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
        driverResolver.Setup(r => r.Resolve(It.IsAny<Ulid>())).Returns((type, config));

        // Provide dummy credentials so BuildS3 does not reject the config for
        // lacking credentials — these tests verify config parsing, not live S3 access.
        Mock<ICredentialResolver> credResolver = new();
        credResolver
            .Setup(r => r.Resolve(It.IsAny<string>()))
            .Returns(("test-access-key", "test-secret-key"));

        return new StorageFactory(
            new LocalStorageDriver(),
            NullLogger<StorageFactory>.Instance,
            driverResolver.Object,
            credResolver.Object
        );
    }

    [Fact]
    public void S3DriverConfig_missing_bucket_throws()
    {
        string json = """{"region":"us-east-1"}""";
        StorageFactory factory = FactoryWithConfig("s3", json);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*bucket*");
    }

    [Fact]
    public void S3DriverConfig_missing_region_throws()
    {
        string json = """{"bucket":"mybucket"}""";
        StorageFactory factory = FactoryWithConfig("s3", json);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*region*");
    }

    [Fact]
    public void R2_without_endpoint_throws()
    {
        string json = """{"bucket":"mybucket","region":"auto"}""";
        StorageFactory factory = FactoryWithConfig("r2", json);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*endpoint*");
    }

    [Fact]
    public void Null_configJson_for_s3_throws()
    {
        StorageFactory factory = FactoryWithConfig("s3", null);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Malformed_configJson_for_s3_throws_ArgumentException()
    {
        StorageFactory factory = FactoryWithConfig("s3", "not-json{{{");
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void S3_with_valid_config_returns_RemoteStorage()
    {
        string json =
            """{"bucket":"test","region":"us-east-1","endpoint":"http://localhost:9000"}""";
        StorageFactory factory = FactoryWithConfig("s3", json);
        Ulid driverId = Ulid.NewUlid();

        IStorage storage = factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void R2_with_endpoint_returns_RemoteStorage()
    {
        string json =
            """{"bucket":"test","region":"auto","endpoint":"https://account.r2.cloudflarestorage.com"}""";
        StorageFactory factory = FactoryWithConfig("r2", json);
        Ulid driverId = Ulid.NewUlid();

        IStorage storage = factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void For_s3_r2_no_longer_throws_NotSupportedException()
    {
        string json =
            """{"bucket":"test","region":"us-east-1","endpoint":"http://localhost:9000"}""";
        StorageFactory factory = FactoryWithConfig("s3", json);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().NotThrow<NotSupportedException>();
    }
}

// ============================================================================
// Integration tests — run against the shared all-in-one storage container
// (MinIO S3). The container is started once for the whole assembly by the
// StorageBackends collection fixture and torn down after the last test.
// ============================================================================

[Collection("StorageBackends")]
public class S3StorageDriverIntegrationTests(StorageBackendsFixture fix)
{
    private string SkipReason => fix.StartupError ?? "storage container not available";

    [SkippableFact]
    public async Task RoundTrip_write_read_delete()
    {
        Skip.If(!fix.Available, SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string path = $"roundtrip/{Ulid.NewUlid()}.txt";
        byte[] data = "hello s3"u8.ToArray();

        // Write
        await using (Stream w = backend.OpenWrite(path, overwrite: true))
        {
            await w.WriteAsync(data);
        }

        // Read
        await using Stream r = backend.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);
        ms.ToArray().Should().Equal(data);

        // Delete
        backend.DeleteFile(path);
        backend.FileExists(path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Multipart_upload_large_file()
    {
        Skip.If(!fix.Available, SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string path = $"large/{Ulid.NewUlid()}.bin";

        // 6 MB — exceeds the 5 MB multipart threshold
        byte[] data = new byte[6 * 1024 * 1024];
        new Random(42).NextBytes(data);

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
        {
            await w.WriteAsync(data);
        }

        long size = backend.GetFileSize(path);
        size.Should().Be(data.Length);

        backend.DeleteFile(path);
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_prefix()
    {
        Skip.If(!fix.Available, SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver("enum-test");
        string prefix = $"dir-{Ulid.NewUlid()}";

        // Write two files under the prefix
        string fileA = $"{prefix}/a.txt";
        string fileB = $"{prefix}/b.txt";
        byte[] bytes = "x"u8.ToArray();

        await using (Stream w = backend.OpenWrite(fileA, overwrite: true))
            await w.WriteAsync(bytes);
        await using (Stream w2 = backend.OpenWrite(fileB, overwrite: true))
            await w2.WriteAsync(bytes);

        IEnumerable<string> entries = backend.EnumerateFileSystemEntries(
            prefix,
            "*.txt",
            SearchOption.AllDirectories
        );

        entries.Should().HaveCount(2);

        backend.DeleteDirectory(prefix, recursive: true);
    }

    [SkippableFact]
    public async Task MoveFile_renames_key()
    {
        Skip.If(!fix.Available, SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string src = $"move/{Ulid.NewUlid()}-src.txt";
        string dst = $"move/{Ulid.NewUlid()}-dst.txt";
        byte[] data = "move me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(src, overwrite: true))
            await w.WriteAsync(data);

        backend.MoveFile(src, dst);

        backend.FileExists(src).Should().BeFalse();
        backend.FileExists(dst).Should().BeTrue();

        backend.DeleteFile(dst);
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_key()
    {
        Skip.If(!fix.Available, SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string src = $"copy/{Ulid.NewUlid()}-src.txt";
        string dst = $"copy/{Ulid.NewUlid()}-dst.txt";
        byte[] data = "copy me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(src, overwrite: true))
            await w.WriteAsync(data);

        backend.CopyFile(src, dst, overwrite: true);

        backend.FileExists(src).Should().BeTrue();
        backend.FileExists(dst).Should().BeTrue();

        backend.DeleteFile(src);
        backend.DeleteFile(dst);
    }

    [SkippableFact]
    public async Task OpenRead_large_file_streams_correctly()
    {
        Skip.If(!fix.Available, SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string path = $"stream/{Ulid.NewUlid()}.bin";
        byte[] data = new byte[2 * 1024 * 1024];
        new Random(7).NextBytes(data);

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        await using Stream r = backend.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);

        ms.ToArray().Should().HaveCount(data.Length);
        backend.DeleteFile(path);
    }

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_rejects_existing()
    {
        Skip.If(!fix.Available, SkipReason);

        S3StorageDriver backend = fix.BuildS3Driver();
        string path = $"nooverwrite/{Ulid.NewUlid()}.txt";
        byte[] data = "original"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        Action act = () => backend.OpenWrite(path, overwrite: false);
        act.Should().Throw<IOException>().WithMessage("*overwrite*");

        backend.DeleteFile(path);
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
        return new S3StorageDriver(mockClient.Object, bucket, prefix);
    }

    [Fact]
    public void EnumerateFileSystemEntries_strips_prefix_from_file_keys()
    {
        Mock<IAmazonS3> mock = new();

        ListObjectsV2Response listResponse = new()
        {
            S3Objects = [new S3Object { Key = "media/folder/file.mp3" }],
            CommonPrefixes = ["media/folder/"],
            IsTruncated = false,
        };

        mock.Setup(c =>
                c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.Delimiter == "/"), default)
            )
            .ReturnsAsync(listResponse);

        S3StorageDriver driver = BuildDriver(mock, "test-bucket", "media");

        List<string> entries = driver
            .EnumerateFileSystemEntries(string.Empty, "*", SearchOption.TopDirectoryOnly)
            .ToList();

        entries.Should().Contain("folder/file.mp3");
        entries.Should().Contain("folder");
        entries.Should().NotContain(e => e.StartsWith("media/"));
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

        mock.Setup(c =>
                c.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.Delimiter == "/"), default)
            )
            .ReturnsAsync(listResponse);

        // DirectoryExists call — expects prefix "media/folder/" (ToKey("folder") = "media/folder/")
        ListObjectsV2Response existsResponse = new()
        {
            S3Objects = [new S3Object { Key = "media/folder/track.mp3" }],
            CommonPrefixes = [],
            IsTruncated = false,
        };

        mock.Setup(c =>
                c.ListObjectsV2Async(
                    It.Is<ListObjectsV2Request>(r => r.Prefix == "media/folder/" && r.MaxKeys == 1),
                    default
                )
            )
            .ReturnsAsync(existsResponse);

        S3StorageDriver driver = BuildDriver(mock, "test-bucket", "media");

        List<string> entries = driver
            .EnumerateFileSystemEntries(string.Empty, "*", SearchOption.TopDirectoryOnly)
            .ToList();

        string dirEntry = entries.Single(e => e == "folder");
        bool exists = driver.DirectoryExists(dirEntry);

        exists
            .Should()
            .BeTrue("round-trip from EnumerateFileSystemEntries to DirectoryExists must work");
    }
}
