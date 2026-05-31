using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Narrows an <see cref="OutputPlan"/> down to the entries a single
/// <see cref="DecomposedTask"/> is responsible for. Pulled out of BuildStage so
/// the stage class can focus on command synthesis — the slicing logic is pure
/// data manipulation with no FFmpeg or storage concerns.
/// </summary>
internal static class BuildStageSlicing
{
    /// <summary>
    /// Coordinator-emitted task → plan slice. The decomposed task says which
    /// outputs this invocation should emit; everything else is dropped from
    /// Video / Audio / Thumbnails so they don't add spurious <c>-i</c> inputs
    /// to encodes that won't consume them. Chapters / Drm / Layout are
    /// preserved as metadata across all slices.
    /// </summary>
    public static OutputPlan SliceForTask(OutputPlan plan, DecomposedTask task)
    {
        if (task.Kind == EncodeTaskKind.Whole)
            return SliceForBundle(plan, task);

        VideoOutputPlan[] videoSlice =
            task.Kind == EncodeTaskKind.Video ? PickMany(plan.VideoOutputs, task) : [];

        AudioOutputPlan[] audioSlice =
            task.Kind == EncodeTaskKind.Audio ? PickMany(plan.AudioOutputs, task) : [];

        SubtitleOutputPlan[] subtitleSlice =
            task.Kind == EncodeTaskKind.Subtitle ? PickMany(plan.SubtitleOutputs, task) : [];

        ThumbnailOutputPlan? thumbsSlice =
            task.Kind == EncodeTaskKind.Thumbnails ? plan.Thumbnails : null;

        IReadOnlyList<AcquiredSubtitle>? acquiredSubs =
            task.Kind == EncodeTaskKind.Subtitle ? plan.AcquiredSubtitles : null;

        return plan with
        {
            VideoOutputs = videoSlice,
            AudioOutputs = audioSlice,
            SubtitleOutputs = subtitleSlice,
            Thumbnails = thumbsSlice,
            AcquiredSubtitles = acquiredSubs,
        };
    }

    /// <summary>
    /// A dispatch-time bundle (Whole task with per-kind slice descriptors set)
    /// narrows the plan to just the entries this batch should emit. Null per-
    /// kind descriptor means "all of that kind"; an empty array means "none of
    /// that kind". This is how the coordinator carves a large encode into
    /// resource-friendly ffmpeg invocations sized to host hardware.
    /// </summary>
    public static OutputPlan SliceForBundle(OutputPlan plan, DecomposedTask task)
    {
        VideoOutputPlan[] videoSlice = task.VideoSliceIndexes is { } videoIndexes
            ? PickIndexes(plan.VideoOutputs, videoIndexes)
            : plan.VideoOutputs;

        AudioOutputPlan[] audioSlice = task.AudioSliceIndexes is { } audioIndexes
            ? PickIndexes(plan.AudioOutputs, audioIndexes)
            : plan.AudioOutputs;

        SubtitleOutputPlan[] subtitleSlice = task.SubtitleSliceIndexes is { } subIndexes
            ? PickIndexes(plan.SubtitleOutputs, subIndexes)
            : plan.SubtitleOutputs;

        ThumbnailOutputPlan? thumbsSlice = task.IncludeThumbnails switch
        {
            false => null,
            _ => plan.Thumbnails,
        };

        IReadOnlyList<AcquiredSubtitle>? acquiredSubs =
            subtitleSlice.Length > 0 ? plan.AcquiredSubtitles : null;

        return plan with
        {
            VideoOutputs = videoSlice,
            AudioOutputs = audioSlice,
            SubtitleOutputs = subtitleSlice,
            Thumbnails = thumbsSlice,
            AcquiredSubtitles = acquiredSubs,
        };
    }

    private static T[] PickMany<T>(T[] source, DecomposedTask task)
    {
        if (task.SourceIndexes is { Length: > 0 } indexes)
            return PickIndexes(source, indexes);

        return OneOrEmpty(source, task.OutputIndex);
    }

    private static T[] PickIndexes<T>(T[] source, int[] indexes)
    {
        if (indexes.Length == 0)
            return [];

        List<T> picked = new(indexes.Length);
        foreach (int index in indexes)
        {
            if (index >= 0 && index < source.Length)
                picked.Add(source[index]);
        }
        return picked.ToArray();
    }

    private static T[] OneOrEmpty<T>(T[] source, int index) =>
        index >= 0 && index < source.Length ? [source[index]] : [];
}
