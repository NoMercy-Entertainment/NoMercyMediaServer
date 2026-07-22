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

using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;
using NoMercy.Tests.Storage.Container;
using WebDav;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Unit tests — no Docker needed
// ============================================================================

public class WebDavDriverConfigParsingTests
{
    [Fact]
    public void Parse_missing_url_throws()
    {
        Action act = () =>
            WebDavDriverConfig.Parse(json: """{"ignoreCertErrors":false}""", folderId: Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*url*");
    }

    [Fact]
    public void Parse_empty_url_throws()
    {
        Action act = () => WebDavDriverConfig.Parse(json: """{"url":"  "}""", folderId: Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*url*");
    }

    [Fact]
    public void Parse_null_config_throws()
    {
        Action act = () => WebDavDriverConfig.Parse(json: null!, folderId: Ulid.NewUlid());
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_malformed_json_throws_ArgumentException()
    {
        Action act = () => WebDavDriverConfig.Parse(json: "{bad json{{", folderId: Ulid.NewUlid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_invalid_timeout_throws()
    {
        string json = """{"url":"http://dav.example.com/","timeoutSeconds":0}""";
        Action act = () => WebDavDriverConfig.Parse(json: json, folderId: Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*timeoutSeconds*");
    }

    [Fact]
    public void Parse_minimal_config_uses_defaults()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            json: """{"url":"http://dav.example.com/files/"}""",
            folderId: Ulid.NewUlid()
        );

        config.Url.Should().Be(expected: "http://dav.example.com/files/");
        config.Username.Should().BeNull();
        config.Password.Should().BeNull();
        config.IgnoreCertErrors.Should().BeFalse();
        config.TimeoutSeconds.Should().Be(expected: 30);
    }

    [Fact]
    public void Parse_url_without_trailing_slash_is_normalized()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            json: """{"url":"http://dav.example.com/files"}""",
            folderId: Ulid.NewUlid()
        );

        config.Url.Should().EndWith(expected: "/");
    }

    [Fact]
    public void Parse_ignoreCertErrors_defaults_to_false()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            json: """{"url":"https://self-signed.example.com/"}""",
            folderId: Ulid.NewUlid()
        );

        config.IgnoreCertErrors.Should().BeFalse();
    }

    [Fact]
    public void Parse_ignoreCertErrors_true_accepted()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            json: """{"url":"https://self-signed.example.com/","ignoreCertErrors":true}""",
            folderId: Ulid.NewUlid()
        );

        config.IgnoreCertErrors.Should().BeTrue();
    }

    [Fact]
    public void Parse_custom_timeout_accepted()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            json: """{"url":"http://dav.example.com/","timeoutSeconds":60}""",
            folderId: Ulid.NewUlid()
        );

        config.TimeoutSeconds.Should().Be(expected: 60);
    }

    [Fact]
    public void Parse_legacy_username_field_emits_warning_and_succeeds()
    {
        Mock<ILogger> logger = new();
        string json =
            """{"url":"http://dav.example.com/","username":"alice","passwordRef":"vault/alice"}""";

        WebDavDriverConfig config = WebDavDriverConfig.Parse(json: json, folderId: Ulid.NewUlid(), logger: logger.Object);

        // Legacy fields are ignored — credentials not on the config.
        config.Username.Should().BeNull();
        config.Password.Should().BeNull();

        // Logger should have received a warning.
        logger.Verify(
            expression: l =>
                l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("deprecated")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public void Parse_legacy_bearerTokenRef_emits_warning_and_succeeds()
    {
        Mock<ILogger> logger = new();
        string json = """{"url":"http://dav.example.com/","bearerTokenRef":"tokens/mytoken"}""";

        WebDavDriverConfig config = WebDavDriverConfig.Parse(json: json, folderId: Ulid.NewUlid(), logger: logger.Object);

        config.Username.Should().BeNull();
        config.Password.Should().BeNull();

        logger.Verify(
            expression: l =>
                l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("deprecated")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public void For_helper_sets_username_and_password()
    {
        WebDavDriverConfig config = WebDavDriverConfig.For(
            url: "http://dav.example.com/",
            username: "alice",
            password: "s3cr3t"
        );

        config.Username.Should().Be(expected: "alice");
        config.Password.Should().Be(expected: "s3cr3t");
    }
}

public class WebDavStorageDriverFactoryTests
{
    private static StorageFactory FactoryWithConfig(string type, string? config)
    {
        Mock<IDriverConfigResolver> resolver = new();
        resolver.Setup(expression: r => r.Resolve(It.IsAny<Ulid>())).Returns(value: (type, config));
        return new(driver: new LocalStorageDriver(), logger: NullLogger<StorageFactory>.Instance, driverConfigResolver: resolver.Object);
    }

    [Fact]
    public void For_webdav_without_config_throws_ArgumentException()
    {
        StorageFactory factory = FactoryWithConfig(type: "webdav", config: null);
        Ulid driverId = Ulid.NewUlid();
        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_webdav_missing_url_throws_ArgumentException()
    {
        StorageFactory factory = FactoryWithConfig(type: "webdav", config: """{"ignoreCertErrors":false}""");
        Ulid driverId = Ulid.NewUlid();
        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");
        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*url*");
    }

    [Fact]
    public void For_webdav_valid_config_returns_RemoteStorage()
    {
        string json = """{"url":"http://dav.example.com/files/"}""";
        StorageFactory factory = FactoryWithConfig(type: "webdav", config: json);
        Ulid driverId = Ulid.NewUlid();

        IStorage storage = factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void For_webdav_malformed_config_throws_ArgumentException()
    {
        StorageFactory factory = FactoryWithConfig(type: "webdav", config: "{{{bad json");
        Ulid driverId = Ulid.NewUlid();
        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_webdav_credential_resolver_injects_username_and_password()
    {
        string json = """{"url":"http://dav.example.com/files/"}""";

        Mock<IDriverConfigResolver> driverResolver = new();
        driverResolver.Setup(expression: r => r.Resolve(It.IsAny<Ulid>())).Returns(value: ("webdav", json));

        Mock<ICredentialResolver> credResolver = new();
        credResolver.Setup(expression: r => r.Resolve(It.IsAny<string>())).Returns(value: ("alice", "s3cr3t"));

        StorageFactory factory = new(
            driver: new LocalStorageDriver(),
            logger: NullLogger<StorageFactory>.Instance,
            driverConfigResolver: driverResolver.Object,
            credentialResolver: credResolver.Object
        );

        // Construction must not throw.
        IStorage storage = factory.For(folderId: Ulid.NewUlid(), driverId: Ulid.NewUlid(), subPath: string.Empty);
        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }
}

// ============================================================================
// Integration tests — run against the shared all-in-one storage container
// (WebDAV). The container is started once for the whole assembly by the
// StorageBackends collection fixture and torn down after the last test.
// ============================================================================

[Collection(name: "StorageBackends")]
public class WebDavStorageDriverIntegrationTests(StorageBackendsFixture fix)
{
    private string SkipReason => fix.StartupError ?? "storage container not available";

    [SkippableFact]
    public async Task RoundTrip_write_read_delete()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        WebDavStorageDriver driver = fix.BuildWebDavDriver();
        string path = $"roundtrip-{Ulid.NewUlid()}.txt";
        byte[] data = "hello webdav"u8.ToArray();

        await using (Stream w = driver.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);

        await using Stream r = driver.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);
        ms.ToArray().Should().Equal(elements: data);

        driver.DeleteFile(path: path);
        driver.FileExists(path: path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task LargeFile_write_read()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        WebDavStorageDriver driver = fix.BuildWebDavDriver();
        string path = $"large-{Ulid.NewUlid()}.bin";

        byte[] data = new byte[12 * 1024 * 1024];
        new Random(Seed: 99).NextBytes(buffer: data);

        await using (Stream w = driver.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);

        long size = driver.GetFileSize(path: path);
        size.Should().Be(expected: data.Length);

        driver.DeleteFile(path: path);
    }

    [SkippableFact]
    public async Task Mkcol_recursive_creation()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        WebDavStorageDriver driver = fix.BuildWebDavDriver();
        string dir = $"a-{Ulid.NewUlid()}/b/c";

        driver.CreateDirectory(path: dir);
        driver.DirectoryExists(path: dir).Should().BeTrue();

        driver.DeleteDirectory(path: $"a-{dir.Split(separator: '/')[0]}", recursive: true);
    }

    [SkippableFact]
    public async Task Propfind_enumerate_with_pattern()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        WebDavStorageDriver driver = fix.BuildWebDavDriver();
        string dirName = $"enum-{Ulid.NewUlid()}";
        driver.CreateDirectory(path: dirName);

        string fileA = $"{dirName}/a.txt";
        string fileB = $"{dirName}/b.txt";
        string fileC = $"{dirName}/c.bin";
        byte[] bytes = "x"u8.ToArray();

        await using (Stream w = driver.OpenWrite(path: fileA, overwrite: true))
            await w.WriteAsync(buffer: bytes);
        await using (Stream w2 = driver.OpenWrite(path: fileB, overwrite: true))
            await w2.WriteAsync(buffer: bytes);
        await using (Stream w3 = driver.OpenWrite(path: fileC, overwrite: true))
            await w3.WriteAsync(buffer: bytes);

        IEnumerable<string> entries = driver.EnumerateFileSystemEntries(
            directory: dirName,
            searchPattern: "*.txt",
            option: SearchOption.TopDirectoryOnly
        );

        entries.Should().HaveCount(expected: 2);

        driver.DeleteDirectory(path: dirName, recursive: true);
    }

    [SkippableFact]
    public async Task MoveFile_renames_resource()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        WebDavStorageDriver driver = fix.BuildWebDavDriver();
        string src = $"move-src-{Ulid.NewUlid()}.txt";
        string dst = $"move-dst-{Ulid.NewUlid()}.txt";
        byte[] data = "move me"u8.ToArray();

        await using (Stream w = driver.OpenWrite(path: src, overwrite: true))
            await w.WriteAsync(buffer: data);

        driver.MoveFile(source: src, destination: dst);

        driver.FileExists(path: src).Should().BeFalse();
        driver.FileExists(path: dst).Should().BeTrue();

        driver.DeleteFile(path: dst);
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_resource()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        WebDavStorageDriver driver = fix.BuildWebDavDriver();
        string src = $"copy-src-{Ulid.NewUlid()}.txt";
        string dst = $"copy-dst-{Ulid.NewUlid()}.txt";
        byte[] data = "copy me"u8.ToArray();

        await using (Stream w = driver.OpenWrite(path: src, overwrite: true))
            await w.WriteAsync(buffer: data);

        driver.CopyFile(source: src, destination: dst, overwrite: true);

        driver.FileExists(path: src).Should().BeTrue();
        driver.FileExists(path: dst).Should().BeTrue();

        driver.DeleteFile(path: src);
        driver.DeleteFile(path: dst);
    }

    [SkippableFact]
    public async Task BasicAuth_wrong_password_fails()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        WebDavClient badClient = new(
            @params: new WebDavClientParams
            {
                BaseAddress = new(uriString: fix.WebDavBaseUrl),
                Credentials = new NetworkCredential(userName: "testuser", password: "wrongpassword"),
            }
        );
        WebDavStorageDriver driver = new(client: badClient, baseUrl: fix.WebDavBaseUrl);

        // Propfind on root with wrong creds should return an HTTP error (401/403).
        // Some servers return 401 as non-successful; the driver returns false (not exception)
        // because FileExists/DirectoryExists swallow non-success responses.
        bool result = driver.DirectoryExists(path: "/");
        result.Should().BeFalse(because: "401/403 responses should be treated as 'not found'");
    }
}

// ============================================================================
// Unit tests — EnumerateFileSystemEntries contract (no Docker)
// ============================================================================

public class WebDavEnumerateContractTests
{
    private const string BaseUrl = "https://nas.local/dav/";

    private static WebDavStorageDriver BuildDriver(Mock<IWebDavClient> mockClient)
    {
        return new(client: mockClient.Object, baseUrl: BaseUrl);
    }

    private static WebDavResource MakeResource(string absoluteUri, bool isCollection)
    {
        WebDavResource.Builder builder = new WebDavResource.Builder().WithUri(uri: absoluteUri);
        if (isCollection)
            builder.IsCollection();
        else
            builder.IsNotCollection();
        return builder.Build();
    }

    private static PropfindResponse MakePropfindResponse(
        int statusCode,
        IEnumerable<WebDavResource> resources
    )
    {
        return new(statusCode: statusCode, resources: resources);
    }

    [Fact]
    public void EnumerateFileSystemEntries_returns_relative_paths_not_absolute_uris()
    {
        Mock<IWebDavClient> mock = new();

        PropfindResponse enumResponse = MakePropfindResponse(
            statusCode: 207,
            resources:
            [
                MakeResource(absoluteUri: "https://nas.local/dav/", isCollection: true), // dir itself — skipped
                MakeResource(absoluteUri: "https://nas.local/dav/folder/file.mp3", isCollection: false),
                MakeResource(absoluteUri: "https://nas.local/dav/folder/", isCollection: true),
            ]
        );

        mock.Setup(expression: c =>
                c.Propfind(
                    It.Is<string>(u => u == BaseUrl),
                    It.Is<PropfindParameters>(p =>
                        p.ApplyTo == ApplyTo.Propfind.ResourceAndChildren
                    )
                )
            )
            .ReturnsAsync(value: enumResponse);

        WebDavStorageDriver driver = BuildDriver(mockClient: mock);

        List<string> entries = driver
            .EnumerateFileSystemEntries(directory: string.Empty, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .ToList();

        entries.Should().Contain(expected: "folder/file.mp3");
        entries.Should().Contain(expected: "folder");
        entries.Should().NotContain(predicate: e => e.StartsWith("https://"));
    }

    [Fact]
    public void EnumerateFileSystemEntries_DirectoryExists_roundtrip()
    {
        Mock<IWebDavClient> mock = new();

        PropfindResponse enumResponse = MakePropfindResponse(
            statusCode: 207,
            resources:
            [
                MakeResource(absoluteUri: "https://nas.local/dav/", isCollection: true),
                MakeResource(absoluteUri: "https://nas.local/dav/music/", isCollection: true),
            ]
        );

        mock.Setup(expression: c =>
                c.Propfind(
                    It.Is<string>(u => u == BaseUrl),
                    It.Is<PropfindParameters>(p =>
                        p.ApplyTo == ApplyTo.Propfind.ResourceAndChildren
                    )
                )
            )
            .ReturnsAsync(value: enumResponse);

        // DirectoryExists calls Propfind on "https://nas.local/dav/music/" with ResourceOnly
        PropfindResponse existsResponse = MakePropfindResponse(
            statusCode: 207,
            resources: [MakeResource(absoluteUri: "https://nas.local/dav/music/", isCollection: true)]
        );

        mock.Setup(expression: c =>
                c.Propfind(
                    It.Is<string>(u => u == "https://nas.local/dav/music/"),
                    It.Is<PropfindParameters>(p => p.ApplyTo == ApplyTo.Propfind.ResourceOnly)
                )
            )
            .ReturnsAsync(value: existsResponse);

        WebDavStorageDriver driver = BuildDriver(mockClient: mock);

        List<string> entries = driver
            .EnumerateFileSystemEntries(directory: string.Empty, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .ToList();

        string dirEntry = entries.Single(predicate: e => e == "music");
        bool exists = driver.DirectoryExists(path: dirEntry);

        exists
            .Should()
            .BeTrue(because: "round-trip from EnumerateFileSystemEntries to DirectoryExists must work");
    }
}
