using System.Collections.Concurrent;

namespace NoMercy.Encoder.DiscRipping;

/// <summary>
/// Singleton registry that enforces one active rip per physical drive at a
/// time. Locks are keyed by the drive's volume UUID when available; when the
/// OS cannot return a UUID the device path is used as the fallback key so the
/// guarantee still holds on systems that expose drives by path only.
///
/// <para>
/// Callers acquire a lock with <see cref="TryAcquire"/> before starting a rip
/// and release it via the returned <see cref="DriveLock"/> (which is
/// <see cref="IDisposable"/>). Different drives lock independently, so two
/// discs in two drives rip concurrently without any contention.
/// </para>
/// </summary>
public sealed class DriveLockRegistry
{
    private readonly ConcurrentDictionary<string, byte> _locked = new(
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// Attempts to acquire the lock for <paramref name="driveKey"/>.
    /// </summary>
    /// <param name="driveKey">
    /// Volume UUID preferred; device path used as fallback.
    /// </param>
    /// <param name="driveLock">
    /// On success, a <see cref="DriveLock"/> whose disposal releases the lock.
    /// <c>null</c> when the drive is already locked.
    /// </param>
    /// <returns>
    /// <c>true</c> when the lock was acquired; <c>false</c> when the drive is
    /// already in use.
    /// </returns>
    public bool TryAcquire(string driveKey, out DriveLock? driveLock)
    {
        if (_locked.TryAdd(driveKey, 0))
        {
            driveLock = new DriveLock(driveKey, this);
            return true;
        }

        driveLock = null;
        return false;
    }

    internal void Release(string driveKey)
    {
        _locked.TryRemove(driveKey, out _);
    }
}

/// <summary>
/// Represents an active per-drive rip lock. Disposing this instance releases
/// the lock back to <see cref="DriveLockRegistry"/> so the next rip request
/// on the same drive can proceed.
/// </summary>
public sealed class DriveLock : IDisposable
{
    private readonly string _driveKey;
    private readonly DriveLockRegistry _registry;
    private bool _disposed;

    internal DriveLock(string driveKey, DriveLockRegistry registry)
    {
        _driveKey = driveKey;
        _registry = registry;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _registry.Release(_driveKey);
    }
}
