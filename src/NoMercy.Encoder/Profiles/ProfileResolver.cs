namespace NoMercy.Encoder.Profiles;

using NoMercy.Encoder.Errors;

/// <summary>
/// Result of a <see cref="IProfileResolver.Resolve"/> call.
/// When <see cref="CycleRule"/> is non-null the parent chain contains a cycle
/// and <see cref="Resolved"/> is null — callers must not proceed with encoding.
/// </summary>
public sealed record ProfileResolutionResult(EncodingProfile? Resolved, EncoderRule? CycleRule);

/// <summary>
/// Resolves a profile's full inherited configuration by walking the parent
/// chain and merging scalar / array fields according to the child-wins rule.
/// Storage-agnostic: the caller supplies a lookup callback so the resolver
/// itself has no dependency on EF Core, repositories, or any I/O layer.
/// </summary>
public interface IProfileResolver
{
    /// <summary>
    /// Walk the parent chain starting at <paramref name="child"/> and return a
    /// fully-merged <see cref="EncodingProfile"/>. The merge rules are:
    /// <list type="bullet">
    ///   <item>Scalar fields — child value wins when non-default (non-null, non-zero, non-false,
    ///         non-enum-default); otherwise the nearest ancestor value is used.</item>
    ///   <item>Array fields (<c>VideoOutputs</c>, <c>AudioOutputs</c>, <c>SubtitleOutputs</c>,
    ///         <c>LadderRungs</c>) — treated as REPLACEMENT: child non-null array (even empty)
    ///         wins; only falls back to parent when child's array is null.</item>
    ///   <item>Identity fields (<c>Id</c>, <c>Name</c>, <c>IsBuiltin</c>, <c>ParentId</c>)
    ///         are NEVER inherited — they always belong to the leaf profile.</item>
    /// </list>
    /// A missing parent (lookup returns null) silently ends the chain — this is not an
    /// error. Only a detected cycle returns a <see cref="EncoderRule"/> failure.
    /// </summary>
    /// <param name="child">The profile to resolve. May have a null ParentId.</param>
    /// <param name="parentLookup">
    /// Called for each ParentId encountered. Return null when the profile does not exist
    /// (orphaned reference) — the resolver treats that as "chain ends here".
    /// </param>
    ProfileResolutionResult Resolve(
        EncodingProfile child,
        Func<Ulid, EncodingProfile?> parentLookup
    );
}

/// <inheritdoc />
public sealed class ProfileResolver : IProfileResolver
{
    /// <inheritdoc />
    public ProfileResolutionResult Resolve(
        EncodingProfile child,
        Func<Ulid, EncodingProfile?> parentLookup
    )
    {
        // Fast path — no inheritance needed.
        if (child.ParentId is null)
            return new ProfileResolutionResult(child, null);

        // Collect the full chain: [child, parent, grandparent, ...]
        List<EncodingProfile> chain = [child];
        HashSet<Ulid> visited = [child.Id];
        List<Ulid> visitedOrder = [child.Id];

        EncodingProfile current = child;

        while (current.ParentId is Ulid parentId)
        {
            if (visited.Contains(parentId))
            {
                // Cycle detected — build a readable path string.
                string path = BuildCyclePath(visitedOrder, parentId);
                EncoderRule cycleRule = new(
                    Id: EncoderRuleId.ParentIdCycle,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "parent_id",
                    Message: $"Parent chain forms a cycle: {path}",
                    Fix: "Break the cycle by clearing the parent_id of one profile in the chain."
                );
                return new ProfileResolutionResult(null, cycleRule);
            }

            EncodingProfile? parent = parentLookup(parentId);

            if (parent is null)
                break; // Missing parent — silently end the chain.

            visited.Add(parentId);
            visitedOrder.Add(parentId);
            chain.Add(parent);
            current = parent;
        }

        // Merge from the oldest ancestor down to the child so that child wins.
        // chain[0] = child, chain[^1] = root ancestor.
        // We iterate from root → child, accumulating into a mutable state.
        EncodingProfile resolved = chain[^1]; // start with root ancestor

        for (int i = chain.Count - 2; i >= 0; i--)
        {
            resolved = MergeChildOntoParent(parent: resolved, child: chain[i]);
        }

        // Restore leaf identity — these are NEVER inherited.
        resolved = resolved with
        {
            Id = child.Id,
            Name = child.Name,
            IsBuiltin = child.IsBuiltin,
            ParentId = child.ParentId,
        };

        return new ProfileResolutionResult(resolved, null);
    }

    // ---------------------------------------------------------------------------
    // Merge helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Produce a new profile where <paramref name="child"/> fields override
    /// <paramref name="parent"/> fields when the child value is non-default.
    /// Identity fields (Id, Name, IsBuiltin, ParentId) are copied from child
    /// unchanged — the final Resolve() call overwrites them with the leaf's
    /// values anyway, but keeping them consistent here avoids confusion.
    /// </summary>
    private static EncodingProfile MergeChildOntoParent(
        EncodingProfile parent,
        EncodingProfile child
    )
    {
        return new EncodingProfile(
            Id: child.Id,
            Name: child.Name,
            Format: child.Format != default ? child.Format : parent.Format,
            VideoOutputs: child.VideoOutputs ?? parent.VideoOutputs,
            AudioOutputs: child.AudioOutputs ?? parent.AudioOutputs,
            SubtitleOutputs: child.SubtitleOutputs ?? parent.SubtitleOutputs,
            Thumbnails: child.Thumbnails ?? parent.Thumbnails,
            SegmentDurationSeconds: child.SegmentDurationSeconds != 0
                ? child.SegmentDurationSeconds
                : parent.SegmentDurationSeconds,
            EncodeMode: child.EncodeMode != default ? child.EncodeMode : parent.EncodeMode,
            AutoLadder: child.AutoLadder || parent.AutoLadder,
            AutoDetectCrop: child.AutoDetectCrop || parent.AutoDetectCrop,
            Drm: child.Drm ?? parent.Drm,
            SchemaVersion: child.SchemaVersion != 0 ? child.SchemaVersion : parent.SchemaVersion
        )
        {
            Description = child.Description ?? parent.Description,
            ParentId = child.ParentId,
            IsBuiltin = child.IsBuiltin,
            HardwarePreference =
                child.HardwarePreference != default
                    ? child.HardwarePreference
                    : parent.HardwarePreference,
            BitDepthPolicy =
                child.BitDepthPolicy != default ? child.BitDepthPolicy : parent.BitDepthPolicy,
            HdrPolicy = child.HdrPolicy != default ? child.HdrPolicy : parent.HdrPolicy,
            TonemapAlgorithm = child.TonemapAlgorithm ?? parent.TonemapAlgorithm,
            HdrOptions = child.HdrOptions ?? parent.HdrOptions,
            LadderRungs = child.LadderRungs ?? parent.LadderRungs,
            PublisherKeyFingerprint =
                child.PublisherKeyFingerprint ?? parent.PublisherKeyFingerprint,
            Signature = child.Signature ?? parent.Signature,
            CustomArguments = child.CustomArguments ?? parent.CustomArguments,
        };
    }

    private static string BuildCyclePath(List<Ulid> visitedOrder, Ulid cycleTarget)
    {
        // Build "A → B → C → A" where A is the repeated node.
        System.Text.StringBuilder sb = new();
        bool found = false;

        foreach (Ulid id in visitedOrder)
        {
            if (sb.Length > 0)
                sb.Append(" → ");
            sb.Append(id);

            if (id == cycleTarget)
                found = true;
        }

        // Append the repeated node to close the cycle visually.
        if (found || visitedOrder.Count > 0)
        {
            sb.Append(" → ");
            sb.Append(cycleTarget);
        }

        return sb.ToString();
    }
}
