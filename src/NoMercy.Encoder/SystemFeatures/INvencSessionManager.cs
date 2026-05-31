namespace NoMercy.Encoder.SystemFeatures;

public interface INvencSessionManager
{
    int DetectedSessionLimit { get; }

    bool IsPatchAvailable { get; }

    bool IsPatched { get; }

    bool CanPatch { get; }

    Task<PatchResult> ApplyPatchAsync(CancellationToken ct);
}

public record PatchResult(bool Success, string Message, bool RequiresRestart);
