namespace NoMercy.Storage.Validation;

/// <summary>
/// Raised when <see cref="StoragePathGuard"/> rejects a path. Surfaces
/// to controllers as the <c>output.path_not_allowed</c> runtime-error
/// rule (catalogued in encoder Phase 4.19).
/// </summary>
public sealed class StoragePathNotAllowedException(string attemptedPath, string reason)
    : InvalidOperationException($"output.path_not_allowed: {reason} (path={attemptedPath})")
{
    public string AttemptedPath { get; } = attemptedPath;
    public string Reason { get; } = reason;
}
