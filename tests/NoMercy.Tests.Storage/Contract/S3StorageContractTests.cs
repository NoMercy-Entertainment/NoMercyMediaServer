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
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Remote;

namespace NoMercy.Tests.Storage.Contract;

// ---------------------------------------------------------------------------
// MinIO class-level fixture.
//
// A single MinIO container starts once for all tests in this class.
// The bucket name is randomised per fixture instance so parallel test runs
// can't share state.  Container teardown happens after all tests finish.
// ---------------------------------------------------------------------------

public sealed class S3ContractFixture : IAsyncLifetime
{
    private IContainer? _container;

    public bool Available { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string Bucket { get; private set; } = string.Empty;

    public const string AccessKey = "minioadmin";
    public const string SecretKey = "minioadmin";

    public async Task InitializeAsync()
    {
        if (!await DockerAvailableAsync())
        {
            Available = false;
            return;
        }

        try
        {
            _container = new ContainerBuilder()
                .WithImage("minio/minio:latest")
                .WithPortBinding(9000, assignRandomHostPort: true)
                .WithEnvironment("MINIO_ROOT_USER", AccessKey)
                .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
                .WithCommand("server", "/data")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9000))
                .Build();

            await _container.StartAsync();

            int port = _container.GetMappedPublicPort(9000);
            Endpoint = $"http://localhost:{port}";
            Bucket = $"contract-{Ulid.NewUlid().ToString().ToLowerInvariant()}";
            Available = true;

            using AmazonS3Client bootstrapClient = BuildRawClient();
            await bootstrapClient.PutBucketAsync(Bucket);
        }
        catch
        {
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public AmazonS3Client BuildRawClient()
    {
        AmazonS3Config cfg = new()
        {
            ServiceURL = Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
        };

        return new AmazonS3Client(new BasicAWSCredentials(AccessKey, SecretKey), cfg);
    }

    private static async Task<bool> DockerAvailableAsync()
    {
        try
        {
            using HttpClient http = new();
            http.Timeout = TimeSpan.FromSeconds(3);
            HttpResponseMessage response = await http.GetAsync("http://localhost:2375/info");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            try
            {
                using System.Diagnostics.Process proc = new();
                proc.StartInfo = new System.Diagnostics.ProcessStartInfo("docker", "info")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                proc.Start();
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}

// Required by xUnit so the fixture is shared across the collection.
[CollectionDefinition("S3ContractIntegration")]
public sealed class S3ContractIntegrationCollection : ICollectionFixture<S3ContractFixture> { }

/// <summary>
/// Runs the shared IStorage contract suite against a real MinIO container.
///
/// Design:
///   - <see cref="S3ContractFixture"/> starts one MinIO container for the whole class.
///   - Each test in the base class calls <see cref="CreateStorage"/> then
///     <see cref="DisposeStorage"/> in its own try/finally.
///   - <see cref="CreateStorage"/> calls <c>Skip.If(!_fixture.Available)</c> so every
///     inherited test skips cleanly when Docker is absent — the base class [Fact]
///     attributes remain, meaning the skip shows as "Skipped" rather than "Not Run".
///   - Seed and backend-check helpers use the raw AWS SDK client directly so they
///     don't exercise the abstraction under test.
/// </summary>
[Collection("S3ContractIntegration")]
[Trait("Category", "Integration")]
public sealed class S3StorageContractTests(S3ContractFixture fixture) : IStorageContractTests
{
    // -----------------------------------------------------------------------
    // IStorageContractTests hooks
    // -----------------------------------------------------------------------

    protected override IStorage CreateStorage()
    {
        Skip.If(!fixture.Available, "Docker / MinIO not available — skipping S3 contract test");

        S3StorageDriver driver = new(
            bucket: fixture.Bucket,
            region: "us-east-1",
            prefix: null,
            endpoint: fixture.Endpoint,
            accessKey: S3ContractFixture.AccessKey,
            secretKey: S3ContractFixture.SecretKey
        );

        return new RemoteStorage(driver);
    }

    /// <summary>
    /// Seed bypasses the driver under test — PUT directly via the raw SDK client.
    /// </summary>
    protected override async Task SeedFile(string relativePath, byte[] content)
    {
        using AmazonS3Client client = fixture.BuildRawClient();

        PutObjectRequest request = new()
        {
            BucketName = fixture.Bucket,
            Key = relativePath.TrimStart('/'),
            InputStream = new MemoryStream(content),
            ContentType = "application/octet-stream",
        };

        await client.PutObjectAsync(request);
    }

    /// <summary>
    /// S3 has no real directory objects. Seed a zero-byte "path/" placeholder so
    /// ListAsync sees the prefix and ExistsAsync works for the directory entry.
    /// </summary>
    protected override async Task SeedDirectory(string relativePath)
    {
        using AmazonS3Client client = fixture.BuildRawClient();

        string key = relativePath.TrimStart('/').TrimEnd('/') + "/";

        PutObjectRequest request = new()
        {
            BucketName = fixture.Bucket,
            Key = key,
            InputStream = new MemoryStream(Array.Empty<byte>()),
            ContentType = "application/x-directory",
        };

        await client.PutObjectAsync(request);
    }

    /// <summary>
    /// Verify file existence directly via HeadObject, bypassing the driver.
    /// </summary>
    protected override async Task<bool> BackendHasFile(string relativePath)
    {
        using AmazonS3Client client = fixture.BuildRawClient();

        try
        {
            GetObjectMetadataRequest request = new()
            {
                BucketName = fixture.Bucket,
                Key = relativePath.TrimStart('/'),
            };

            await client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    protected override Task DisposeStorage()
    {
        // RemoteStorage wraps S3StorageDriver which is IDisposable — dispose it.
        // Nothing else to clean; the MinIO container persists across tests.
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // S3-specific overrides — document known driver divergences
    // -----------------------------------------------------------------------

    // Absolute path rejection: RemoteStorage.V() calls StructuralValidate
    // which throws StoragePathNotAllowedException for leading-slash, drive-letter,
    // and UNC paths before the S3 driver is reached.

    // Null-byte rejection: StructuralValidate throws StoragePathNotAllowedException
    // before the SDK is called, satisfying the base contract's "throws" assertion.

    // double-slash normalisation: S3 preserves the double-slash in the key.
    // "foo//bar.bin" != "foo/bar.bin" in S3 key space.
    // The base contract asserts withDouble == withSingle; this WILL FAIL for S3.
    // Documented here as a known failure (separate named test so xUnit1024 is satisfied).
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task S3_double_slash_is_known_failure_requires_driver_normalisation()
    {
        Skip.If(!fixture.Available, "Docker not available");

        IStorage storage = CreateStorage();
        try
        {
            byte[] data = new byte[] { 0x01, 0x02 };
            await SeedFile("foo/bar.bin", data);

            bool withSingle = await storage.ExistsAsync("foo/bar.bin", CancellationToken.None);

            // KNOWN FAILURE: S3StorageDriver does not collapse double slashes before
            // building S3 keys, so "foo//bar.bin" is a distinct key from "foo/bar.bin".
            // This assertion is expected to fail until the driver normalises input paths.
            bool withDouble = await storage.ExistsAsync("foo//bar.bin", CancellationToken.None);

            withSingle.Should().BeTrue();
            withDouble
                .Should()
                .Be(
                    withSingle,
                    "KNOWN FAILURE: S3StorageDriver does not collapse double slashes — driver fix needed"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // List_flat_does_not_see_subdir_contents: S3 LIST with delimiter "/" works
    // correctly. Expected to pass.

    // List_recursive_sees_subdir_contents: S3 LIST without delimiter returns
    // all keys. Expected to pass.
}
