using System.Collections.Concurrent;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Storage;

/// <summary>
/// IStorage decorator that records every call into <see cref="Calls"/>
/// before delegating to an inner implementation. Used by Phase 0.3
/// verification tests to prove encoder code reaches the storage
/// abstraction (instead of dropping to raw System.IO).
/// </summary>
public sealed class LoggingStorage(IStorage inner) : IStorage
{
    public ConcurrentBag<string> Calls { get; } = [];

    public Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        Calls.Add($"ReadAsync:{path}");
        return inner.ReadAsync(path, ct);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        Calls.Add($"OpenReadAsync:{path}");
        return inner.OpenReadAsync(path, ct);
    }

    public Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        Calls.Add($"WriteAsync:{path}");
        return inner.WriteAsync(path, bytes, ct);
    }

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct)
    {
        Calls.Add($"OpenWriteAsync:{path}");
        return inner.OpenWriteAsync(path, overwrite, ct);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        Calls.Add($"ExistsAsync:{path}");
        return inner.ExistsAsync(path, ct);
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        Calls.Add($"DeleteAsync:{path}");
        return inner.DeleteAsync(path, ct);
    }

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct)
    {
        Calls.Add($"DeleteDirectoryAsync:{path}");
        return inner.DeleteDirectoryAsync(path, recursive, ct);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        Calls.Add($"CreateDirectoryAsync:{path}");
        return inner.CreateDirectoryAsync(path, ct);
    }

    public Task MoveAsync(string from, string to, CancellationToken ct)
    {
        Calls.Add($"MoveAsync:{from}→{to}");
        return inner.MoveAsync(from, to, ct);
    }

    public Task CopyAsync(string from, string to, CancellationToken ct)
    {
        Calls.Add($"CopyAsync:{from}→{to}");
        return inner.CopyAsync(from, to, ct);
    }

    public Task<long> SizeAsync(string path, CancellationToken ct)
    {
        Calls.Add($"SizeAsync:{path}");
        return inner.SizeAsync(path, ct);
    }

    public Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct)
    {
        Calls.Add($"LastModifiedAsync:{path}");
        return inner.LastModifiedAsync(path, ct);
    }

    public IAsyncEnumerable<StorageEntry> ListAsync(
        string path,
        string? pattern,
        bool recursive,
        CancellationToken ct
    )
    {
        Calls.Add($"ListAsync:{path}:{pattern}:{recursive}");
        return inner.ListAsync(path, pattern, recursive, ct);
    }

    public Task<string> HashAsync(string path, string algorithm, CancellationToken ct)
    {
        Calls.Add($"HashAsync:{path}:{algorithm}");
        return inner.HashAsync(path, algorithm, ct);
    }

    public Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct)
    {
        Calls.Add($"AcquireLocalPathAsync:{path}");
        return inner.AcquireLocalPathAsync(path, ct);
    }

    // --- Sync companions ----------------------------------------------------

    public bool Exists(string path)
    {
        Calls.Add($"Exists:{path}");
        return inner.Exists(path);
    }

    public long SizeOrZero(string path)
    {
        Calls.Add($"SizeOrZero:{path}");
        return inner.SizeOrZero(path);
    }

    public long Size(string path)
    {
        Calls.Add($"Size:{path}");
        return inner.Size(path);
    }

    public DateTimeOffset LastModified(string path)
    {
        Calls.Add($"LastModified:{path}");
        return inner.LastModified(path);
    }

    public void CreateDirectory(string path)
    {
        Calls.Add($"CreateDirectory:{path}");
        inner.CreateDirectory(path);
    }

    public void Delete(string path)
    {
        Calls.Add($"Delete:{path}");
        inner.Delete(path);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        Calls.Add($"DeleteDirectory:{path}");
        inner.DeleteDirectory(path, recursive);
    }

    public byte[] Read(string path)
    {
        Calls.Add($"Read:{path}");
        return inner.Read(path);
    }

    public Stream OpenRead(string path)
    {
        Calls.Add($"OpenRead:{path}");
        return inner.OpenRead(path);
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        Calls.Add($"OpenWrite:{path}");
        return inner.OpenWrite(path, overwrite);
    }

    public void Write(string path, byte[] bytes)
    {
        Calls.Add($"Write:{path}");
        inner.Write(path, bytes);
    }

    public void Move(string from, string to)
    {
        Calls.Add($"Move:{from}→{to}");
        inner.Move(from, to);
    }

    public void Copy(string from, string to)
    {
        Calls.Add($"Copy:{from}→{to}");
        inner.Copy(from, to);
    }

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive)
    {
        Calls.Add($"List:{path}:{pattern}:{recursive}");
        return inner.List(path, pattern, recursive);
    }

    public LocalPathLease AcquireLocalPath(string path)
    {
        Calls.Add($"AcquireLocalPath:{path}");
        return inner.AcquireLocalPath(path);
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct)
    {
        Calls.Add($"ReadAllTextAsync:{path}");
        return inner.ReadAllTextAsync(path, ct);
    }

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        Calls.Add($"WriteAllTextAsync:{path}");
        return inner.WriteAllTextAsync(path, contents, ct);
    }

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct)
    {
        Calls.Add($"MoveDirectoryAsync:{from}→{to}");
        return inner.MoveDirectoryAsync(from, to, ct);
    }

    public void MoveDirectory(string from, string to)
    {
        Calls.Add($"MoveDirectory:{from}→{to}");
        inner.MoveDirectory(from, to);
    }
}
