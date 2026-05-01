using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace NoMercy.Storage.Backends.S3;

/// <summary>
/// <see cref="IStorageBackend"/> backed by any S3-compatible object store
/// (AWS S3, Cloudflare R2, MinIO, DigitalOcean Spaces, …).
/// </summary>
public sealed class S3StorageBackend : IStorageBackend, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly string _prefix;

    /// <param name="bucket">S3 bucket name.</param>
    /// <param name="region">AWS region string (e.g. <c>us-east-1</c>).</param>
    /// <param name="prefix">
    ///   Optional key prefix prepended to every path. A trailing slash is
    ///   added automatically so callers never need to include it.
    /// </param>
    /// <param name="endpoint">
    ///   Optional service URL override. Required for non-AWS providers
    ///   (R2, MinIO, Spaces). <c>ForcePathStyle</c> is enabled automatically
    ///   when this is set.
    /// </param>
    /// <param name="accessKey">AWS access key ID. When null, the default
    ///   credential chain is used (env-vars / IAM role).</param>
    /// <param name="secretKey">AWS secret access key. Required when
    ///   <paramref name="accessKey"/> is supplied.</param>
    public S3StorageBackend(
        string bucket,
        string region,
        string? prefix = null,
        string? endpoint = null,
        string? accessKey = null,
        string? secretKey = null
    )
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("bucket must not be empty", nameof(bucket));
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("region must not be empty", nameof(region));

        _bucket = bucket;
        _prefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix.TrimEnd('/') + "/";

        AmazonS3Config config = new() { RegionEndpoint = RegionEndpoint.GetBySystemName(region) };

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            config.ServiceURL = endpoint;
            config.ForcePathStyle = true;
        }

        if (accessKey is not null && secretKey is not null)
        {
            _client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
        }
        else
        {
            _client = new AmazonS3Client(config);
        }
    }

    /// <summary>
    /// Exposed for testing — allows injection of a pre-configured client
    /// (e.g. one pointing at a Testcontainers MinIO instance).
    /// </summary>
    internal S3StorageBackend(IAmazonS3 client, string bucket, string? prefix = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        _prefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix.TrimEnd('/') + "/";
    }

    // -----------------------------------------------------------------------
    // Key helpers
    // -----------------------------------------------------------------------

    private string ToKey(string path) =>
        _prefix + path.TrimStart('/').TrimStart('\\').Replace('\\', '/');

    private string FromKey(string key) =>
        string.IsNullOrEmpty(_prefix) ? key : key.Substring(_prefix.Length);

    // -----------------------------------------------------------------------
    // IStorageBackend
    // -----------------------------------------------------------------------

    public bool FileExists(string path)
    {
        string key = ToKey(path);
        try
        {
            GetObjectMetadataRequest request = new() { BucketName = _bucket, Key = key };
            _client.GetObjectMetadataAsync(request).GetAwaiter().GetResult();
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public bool DirectoryExists(string path)
    {
        string prefix = ToKey(path).TrimEnd('/') + "/";
        ListObjectsV2Request request = new()
        {
            BucketName = _bucket,
            Prefix = prefix,
            MaxKeys = 1,
        };
        ListObjectsV2Response response = _client
            .ListObjectsV2Async(request)
            .GetAwaiter()
            .GetResult();
        return response.S3Objects.Count > 0 || response.CommonPrefixes.Count > 0;
    }

    public void CreateDirectory(string path)
    {
        // S3 has no real directories. Optionally PUT a zero-byte placeholder
        // so directory-listing tools see it. This is a no-op in practice.
        string key = ToKey(path).TrimEnd('/') + "/";
        PutObjectRequest request = new()
        {
            BucketName = _bucket,
            Key = key,
            ContentBody = string.Empty,
        };
        _client.PutObjectAsync(request).GetAwaiter().GetResult();
    }

    public void DeleteFile(string path)
    {
        string key = ToKey(path);
        DeleteObjectRequest request = new() { BucketName = _bucket, Key = key };
        _client.DeleteObjectAsync(request).GetAwaiter().GetResult();
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        if (!recursive)
        {
            // Non-recursive: remove only the zero-byte placeholder if present.
            string key = ToKey(path).TrimEnd('/') + "/";
            DeleteObjectRequest request = new() { BucketName = _bucket, Key = key };
            _client.DeleteObjectAsync(request).GetAwaiter().GetResult();
            return;
        }

        string prefix = ToKey(path).TrimEnd('/') + "/";
        string? continuationToken = null;

        do
        {
            ListObjectsV2Request listRequest = new()
            {
                BucketName = _bucket,
                Prefix = prefix,
                MaxKeys = 1000,
                ContinuationToken = continuationToken,
            };
            ListObjectsV2Response listResponse = _client
                .ListObjectsV2Async(listRequest)
                .GetAwaiter()
                .GetResult();

            if (listResponse.S3Objects.Count > 0)
            {
                DeleteObjectsRequest deleteRequest = new()
                {
                    BucketName = _bucket,
                    Objects = listResponse
                        .S3Objects.Select(o => new KeyVersion { Key = o.Key })
                        .ToList(),
                };
                _client.DeleteObjectsAsync(deleteRequest).GetAwaiter().GetResult();
            }

            continuationToken =
                listResponse.IsTruncated == true ? listResponse.NextContinuationToken : null;
        } while (continuationToken is not null);
    }

    public long GetFileSize(string path)
    {
        string key = ToKey(path);
        GetObjectMetadataRequest request = new() { BucketName = _bucket, Key = key };
        GetObjectMetadataResponse response = _client
            .GetObjectMetadataAsync(request)
            .GetAwaiter()
            .GetResult();
        return response.ContentLength;
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        string key = ToKey(path);
        GetObjectMetadataRequest request = new() { BucketName = _bucket, Key = key };
        GetObjectMetadataResponse response = _client
            .GetObjectMetadataAsync(request)
            .GetAwaiter()
            .GetResult();
        return response.LastModified.ToUniversalTime();
    }

    public Stream OpenRead(string path)
    {
        string key = ToKey(path);
        GetObjectRequest request = new() { BucketName = _bucket, Key = key };
        GetObjectResponse response = _client.GetObjectAsync(request).GetAwaiter().GetResult();
        return response.ResponseStream;
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        if (!overwrite && FileExists(path))
            throw new IOException(
                $"Cannot write to '{path}': the key already exists and overwrite is false."
            );

        string key = ToKey(path);
        return new S3UploadStream(_client, _bucket, key);
    }

    public void MoveFile(string source, string destination)
    {
        CopyObjectRequest copyRequest = new()
        {
            SourceBucket = _bucket,
            SourceKey = ToKey(source),
            DestinationBucket = _bucket,
            DestinationKey = ToKey(destination),
        };
        _client.CopyObjectAsync(copyRequest).GetAwaiter().GetResult();
        DeleteFile(source);
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        if (!overwrite && FileExists(destination))
            throw new IOException(
                $"Cannot copy to '{destination}': the key already exists and overwrite is false."
            );

        CopyObjectRequest request = new()
        {
            SourceBucket = _bucket,
            SourceKey = ToKey(source),
            DestinationBucket = _bucket,
            DestinationKey = ToKey(destination),
        };
        _client.CopyObjectAsync(request).GetAwaiter().GetResult();
    }

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        string prefix = ToKey(directory).TrimEnd('/');
        if (!string.IsNullOrEmpty(prefix))
            prefix += "/";

        bool recursive = option == SearchOption.AllDirectories;
        string? continuationToken = null;

        List<string> results = [];

        do
        {
            ListObjectsV2Request request = new()
            {
                BucketName = _bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken,
                MaxKeys = 1000,
            };

            if (!recursive)
                request.Delimiter = "/";

            ListObjectsV2Response response = _client
                .ListObjectsV2Async(request)
                .GetAwaiter()
                .GetResult();

            foreach (S3Object obj in response.S3Objects)
            {
                if (obj.Key.EndsWith("/", StringComparison.Ordinal))
                    continue;

                string relPath = FromKey(obj.Key);
                string fileName = relPath.Contains('/')
                    ? relPath.Substring(relPath.LastIndexOf('/') + 1)
                    : relPath;

                if (MatchesPattern(fileName, searchPattern))
                    results.Add(obj.Key);
            }

            if (!recursive)
            {
                foreach (string commonPrefix in response.CommonPrefixes)
                {
                    string relPath = FromKey(commonPrefix.TrimEnd('/'));
                    string dirName = relPath.Contains('/')
                        ? relPath.Substring(relPath.LastIndexOf('/') + 1)
                        : relPath;

                    if (MatchesPattern(dirName, searchPattern))
                        results.Add(commonPrefix);
                }
            }

            continuationToken =
                response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuationToken is not null);

        return results;
    }

    public string GetFullPath(string path)
    {
        string normalized = path.Replace('\\', '/').Trim('/');
        return string.IsNullOrEmpty(_prefix) ? normalized : _prefix.TrimEnd('/') + "/" + normalized;
    }

    public string? ResolveLinkTarget(string path) => null;

    public bool IsHidden(string path) => false;

    public void MoveDirectory(string source, string destination)
    {
        string srcPrefix = ToKey(source).TrimEnd('/') + "/";
        string dstPrefix = ToKey(destination).TrimEnd('/') + "/";
        string? continuationToken = null;

        do
        {
            ListObjectsV2Request listRequest = new()
            {
                BucketName = _bucket,
                Prefix = srcPrefix,
                MaxKeys = 1000,
                ContinuationToken = continuationToken,
            };
            ListObjectsV2Response listResponse = _client
                .ListObjectsV2Async(listRequest)
                .GetAwaiter()
                .GetResult();

            foreach (S3Object obj in listResponse.S3Objects)
            {
                string newKey = dstPrefix + obj.Key.Substring(srcPrefix.Length);
                CopyObjectRequest copyRequest = new()
                {
                    SourceBucket = _bucket,
                    SourceKey = obj.Key,
                    DestinationBucket = _bucket,
                    DestinationKey = newKey,
                };
                _client.CopyObjectAsync(copyRequest).GetAwaiter().GetResult();
            }

            if (listResponse.S3Objects.Count > 0)
            {
                DeleteObjectsRequest deleteRequest = new()
                {
                    BucketName = _bucket,
                    Objects = listResponse
                        .S3Objects.Select(o => new KeyVersion { Key = o.Key })
                        .ToList(),
                };
                _client.DeleteObjectsAsync(deleteRequest).GetAwaiter().GetResult();
            }

            continuationToken =
                listResponse.IsTruncated == true ? listResponse.NextContinuationToken : null;
        } while (continuationToken is not null);
    }

    public void Dispose() => _client.Dispose();

    // -----------------------------------------------------------------------
    // Pattern matching (glob → regex)
    // -----------------------------------------------------------------------

    private static bool MatchesPattern(string name, string pattern)
    {
        if (pattern == "*" || string.IsNullOrEmpty(pattern))
            return true;

        // Convert simple glob (* and ?) to regex
        string regexPattern =
            "^"
            + string.Concat(
                pattern.Select(c =>
                    c switch
                    {
                        '*' => ".*",
                        '?' => ".",
                        '.' => "\\.",
                        _ => System.Text.RegularExpressions.Regex.Escape(c.ToString()),
                    }
                )
            )
            + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            name,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
    }
}
