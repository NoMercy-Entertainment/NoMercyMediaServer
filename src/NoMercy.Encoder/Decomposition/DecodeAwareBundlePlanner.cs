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

using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.Decomposition;

/// <summary>
/// Packs a decomposed single-pass task list into decode-cost-driven bundles.
/// A full ffmpeg decode is the real cost of an encode, not the rung count —
/// this planner is a two-layer model that keeps that distinction explicit
/// and independently testable.
///
/// <para><b>Layer 1 — decode grouping</b> (<see cref="GroupByDecodeClass"/>):
/// classifies every Video output by <see cref="DecodeClass"/>. Copy needs no
/// decode at all. Transcode rungs off the same source share ONE decode via
/// the filtergraph split the builder already emits. Tonemap rungs share
/// their own single HDR→SDR pass (and the thumbnail sprite, for correct
/// Rec.709 color) via the same mechanism — kept as a separate class from
/// Transcode so a mixed HDR-preserve + SDR-tonemap ladder gets two distinct
/// decode groups rather than silently sharing one.</para>
///
/// <para><b>Layer 2 — capacity scheduling</b> (inside <see cref="Plan"/>):
/// each decode group becomes ONE bundle by default — one ffmpeg, one decode.
/// A group is split into multiple bundles only when host/encoder capacity
/// (<paramref name="gpuCap"/> / <paramref name="cpuCap"/> in <see cref="Plan"/>)
/// forces it, and only on decode-independent boundaries — every split bundle
/// re-decodes (and, for a Tonemap group, re-tonemaps) its own share. This is
/// the future distributed-encoding knob and is deliberately kept separate
/// from Layer 1's classification.</para>
///
/// <para>Copy video / audio / subtitle / chapters never need their own
/// decode: they ride the first real decode bundle produced (Tonemap first,
/// then Transcode) for free. When the plan has NO real decode group at all
/// — a pure remux, or no video whatsoever — they form one zero-decode
/// "remux" bundle instead. The thumbnail sprite follows the same priority
/// (Tonemap bundle, else the first Transcode bundle) but NEVER rides the
/// zero-decode remux bundle: with no real decode group it becomes its own
/// standalone bundle, matching the standalone sprite command BuildStage
/// already builds for an all-copy plan (see the HDR tonemap fix on that
/// path — a copy-source sprite still needs correct Rec.709 color).</para>
/// </summary>
public static class DecodeAwareBundlePlanner
{
    /// <summary>
    /// Layer 1: partitions every Video-kind task in <paramref name="tasks"/>
    /// into decode-sharing groups. Pure classification — no capacity concern,
    /// no bundle construction — kept separate so the decode-grouping decision
    /// can be asserted directly, independent of how many ffmpeg invocations
    /// host capacity later carves a group into.
    /// </summary>
    public static IReadOnlyList<DecodeGroup> GroupByDecodeClass(
        DecomposedTask[] tasks,
        OutputPlan plan
    )
    {
        List<int> copyIndexes = new();
        List<int> transcodeIndexes = new();
        List<string> tonemapChainOrder = new();
        Dictionary<string, List<int>> tonemapByChain = new(StringComparer.Ordinal);

        for (int i = 0; i < tasks.Length; i++)
        {
            DecomposedTask task = tasks[i];
            if (task.Kind != EncodeTaskKind.Video)
                continue;

            if (string.Equals(task.VideoEncoderName, "copy", StringComparison.OrdinalIgnoreCase))
            {
                copyIndexes.Add(i);
                continue;
            }

            VideoOutputPlan? output =
                task.OutputIndex >= 0 && task.OutputIndex < plan.VideoOutputs.Length
                    ? plan.VideoOutputs[task.OutputIndex]
                    : null;

            bool isTonemap =
                output is { ConvertHdrToSdr: true }
                && !string.IsNullOrEmpty(output.TonemapFilterChain);

            if (isTonemap)
            {
                string chain = output!.TonemapFilterChain!;
                if (!tonemapByChain.TryGetValue(chain, out List<int>? chainIndexes))
                {
                    chainIndexes = new();
                    tonemapByChain[chain] = chainIndexes;
                    tonemapChainOrder.Add(chain);
                }
                chainIndexes.Add(i);
            }
            else
            {
                transcodeIndexes.Add(i);
            }
        }

        List<DecodeGroup> groups = new();
        if (copyIndexes.Count > 0)
            groups.Add(new(DecodeClass.Copy, TonemapChain: null, copyIndexes));
        if (transcodeIndexes.Count > 0)
            groups.Add(new(DecodeClass.Transcode, TonemapChain: null, transcodeIndexes));
        foreach (string chain in tonemapChainOrder)
            groups.Add(new(DecodeClass.Tonemap, chain, tonemapByChain[chain]));

        return groups;
    }

    /// <summary>
    /// Full two-layer plan: groups by decode class (Layer 1), splits each
    /// real-decode group by host capacity (Layer 2), attaches copy video /
    /// audio / subtitle / chapters / the thumbnail sprite to the first real
    /// decode bundle (or a standalone zero-decode bundle when none exists),
    /// and returns the resulting <see cref="EncodeTaskKind.Whole"/> bundles
    /// ready for sequential dispatch.
    /// </summary>
    public static DecomposedTask[] Plan(
        DecomposedTask[] tasks,
        OutputPlan plan,
        int parentJobId,
        string groupTag,
        int gpuCap,
        int cpuCap,
        bool isPartialTopUp = false
    )
    {
        DecomposedTask[] stamped = tasks
            .Select(task => task with { ParentJobId = parentJobId })
            .ToArray();

        IReadOnlyList<DecodeGroup> decodeGroups = GroupByDecodeClass(stamped, plan);

        int[] copyVideoIndexes =
            decodeGroups
                .FirstOrDefault(group => group.Class == DecodeClass.Copy)
                ?.VideoTaskIndexes.ToArray()
            ?? [];

        // Transcode (HDR-preserve 4K master) bundles are ordered BEFORE Tonemap
        // (SDR) bundles: the 4K HDR master must land first so native-HDR clients
        // (Android / Apple) can play the moment it exists, and — since each
        // bundle is one ffmpeg dispatched sequentially — the host never runs the
        // heavy 4K encode and the tonemap+downscale at the same time. The SDR
        // rungs derive from the 4K master (see the cascade wiring below), so
        // their order after it is also their data dependency.
        List<(int[] Video, bool IsTonemap)> orderedRealUnits = new();
        foreach (DecodeGroup group in decodeGroups.Where(g => g.Class == DecodeClass.Transcode))
        foreach (int[] chunk in ChunkByCapacity(group.VideoTaskIndexes, stamped, gpuCap, cpuCap))
            orderedRealUnits.Add((chunk, false));
        foreach (DecodeGroup group in decodeGroups.Where(g => g.Class == DecodeClass.Tonemap))
        foreach (int[] chunk in ChunkByCapacity(group.VideoTaskIndexes, stamped, gpuCap, cpuCap))
            orderedRealUnits.Add((chunk, true));

        bool hasThumbs = stamped.Any(task => task.Kind == EncodeTaskKind.Thumbnails);
        int[] audioIndexes = IndexesOf(stamped, task => task.Kind == EncodeTaskKind.Audio);
        int[] subIndexes = IndexesOf(stamped, task => task.Kind == EncodeTaskKind.Subtitle);
        int[] chapterIndexes = IndexesOf(stamped, task => task.Kind == EncodeTaskKind.Chapters);

        // The thumbnail sprite must be Rec.709 (SDR), so it rides a Tonemap
        // bundle to reuse its HDR→SDR pass instead of sampling raw HDR. With the
        // 4K master now first, the first bundle is HDR-preserve — putting the
        // sprite there would crush its colours. Route it to the first Tonemap
        // bundle; fall back to bundle 0 only when the plan has no tonemap at all
        // (a pure-SDR ladder, where bundle 0 is already Rec.709).
        int firstTonemapUnit = orderedRealUnits.FindIndex(unit => unit.IsTonemap);
        int spriteUnit = firstTonemapUnit >= 0 ? firstTonemapUnit : 0;

        List<DecomposedTask> bundles = new();
        bool auxAttached = orderedRealUnits.Count > 0;
        int bundleIndex = 0;

        for (int unitIndex = 0; unitIndex < orderedRealUnits.Count; unitIndex++)
        {
            int[] videoChunk = orderedRealUnits[unitIndex].Video;
            int[] videoForBundle = videoChunk;
            int[] audioForBundle = [];
            int[] subsForBundle = [];
            bool thumbsForBundle = false;
            int[] chaptersForBundle = [];

            if (unitIndex == 0)
            {
                // Bundle 0 (the 4K HDR master) rides everything that needs no
                // decode of its own: copy video (free — this ffmpeg already has
                // the stream open) and every audio / subtitle / chapter task, so
                // the master rendition is immediately playable with sound.
                videoForBundle = videoChunk.Concat(copyVideoIndexes).ToArray();
                audioForBundle = audioIndexes;
                subsForBundle = subIndexes;
                chaptersForBundle = chapterIndexes;
            }

            if (hasThumbs && unitIndex == spriteUnit)
                thumbsForBundle = true;

            bundles.Add(
                BuildBundleTask(
                    stamped,
                    videoForBundle,
                    audioForBundle,
                    subsForBundle,
                    thumbsForBundle,
                    chaptersForBundle,
                    parentJobId,
                    groupTag,
                    bundleIndex++,
                    isPartialTopUp
                )
            );
        }

        if (!auxAttached)
        {
            // No real decode group exists — either the whole plan is
            // stream-copy (remux) or there is no video at all (an audio/
            // subtitle-only profile). Copy video + audio + subtitle +
            // chapters form ONE zero-decode "remux" bundle.
            bool hasRemuxContent =
                copyVideoIndexes.Length > 0
                || audioIndexes.Length > 0
                || subIndexes.Length > 0
                || chapterIndexes.Length > 0;

            if (hasRemuxContent)
            {
                bundles.Add(
                    BuildBundleTask(
                        stamped,
                        copyVideoIndexes,
                        audioIndexes,
                        subIndexes,
                        thumbsForBundle: false,
                        chapterIndexes,
                        parentJobId,
                        groupTag,
                        bundleIndex++,
                        isPartialTopUp
                    )
                );
            }

            // The sprite never rides the zero-decode remux bundle: with no
            // filtergraph-fed video output on that bundle's sliced plan,
            // BuildStage routes the sprite through its own standalone
            // command anyway (HasFiltergraphVideoOutput is false whenever
            // VideoOutputs is empty or all-copy) — giving it a dedicated
            // bundle here keeps that a separate, independently retryable
            // unit of work instead of an implicit side effect of the remux.
            if (hasThumbs)
            {
                bundles.Add(
                    BuildBundleTask(
                        stamped,
                        videoIndexes: [],
                        audioIndexes: [],
                        subIndexes: [],
                        thumbsForBundle: true,
                        chapterIndexes: [],
                        parentJobId,
                        groupTag,
                        bundleIndex++,
                        isPartialTopUp
                    )
                );
            }
        }

        return bundles.ToArray();
    }

    /// <summary>
    /// Layer 2: splits one decode group's video task indexes into capacity-
    /// bounded units. GPU-encoded and CPU-encoded rungs are always split into
    /// separate units — they draw from different resource pools with
    /// different caps, so mixing them in one accounting bucket would let one
    /// pool starve the other. Each returned unit is a decode-independent
    /// boundary: a group split into N units means N separate ffmpeg
    /// invocations, each re-decoding (and, for a Tonemap group, re-
    /// tonemapping) its own share.
    /// </summary>
    private static List<int[]> ChunkByCapacity(
        IReadOnlyList<int> videoIndexes,
        DecomposedTask[] tasks,
        int gpuCap,
        int cpuCap
    )
    {
        List<int> gpuIndexes = videoIndexes
            .Where(idx => tasks[idx].Resources?.GpuDeviceKey is not null)
            .ToList();
        List<int> cpuIndexes = videoIndexes
            .Where(idx => tasks[idx].Resources?.GpuDeviceKey is null)
            .ToList();

        List<int[]> chunks = new();
        chunks.AddRange(Chunk(gpuIndexes, gpuCap));
        chunks.AddRange(Chunk(cpuIndexes, cpuCap));
        return chunks;
    }

    private static List<int[]> Chunk(List<int> indexes, int cap)
    {
        List<int[]> chunks = new();
        if (indexes.Count == 0 || cap <= 0)
            return chunks;

        for (int offset = 0; offset < indexes.Count; offset += cap)
            chunks.Add(indexes.Skip(offset).Take(cap).ToArray());

        return chunks;
    }

    private static int[] IndexesOf(DecomposedTask[] tasks, Func<DecomposedTask, bool> predicate)
    {
        List<int> indexes = new();
        for (int i = 0; i < tasks.Length; i++)
        {
            if (predicate(tasks[i]))
                indexes.Add(i);
        }
        return indexes.ToArray();
    }

    private static DecomposedTask BuildBundleTask(
        DecomposedTask[] allTasks,
        int[] videoIndexes,
        int[] audioIndexes,
        int[] subIndexes,
        bool thumbsForBundle,
        int[] chapterIndexes,
        int parentJobId,
        string groupTag,
        int bundleIndex,
        bool isPartialTopUp
    )
    {
        int[] containedTaskIndexes = videoIndexes
            .Concat(audioIndexes)
            .Concat(subIndexes)
            .Concat(chapterIndexes)
            .ToArray();

        if (thumbsForBundle)
        {
            int[] thumbnailTaskIndex = IndexesOf(
                allTasks,
                task => task.Kind == EncodeTaskKind.Thumbnails
            );
            containedTaskIndexes = containedTaskIndexes.Concat(thumbnailTaskIndex).ToArray();
        }

        string[] bundledIds = containedTaskIndexes.Select(idx => allTasks[idx].TaskId).ToArray();

        // Video/audio/subtitle slice descriptors point at the parent plan's
        // OutputPlan.{Video,Audio,Subtitle}Outputs indexes (DecomposedTask.OutputIndex
        // on each per-stream task is that index).
        int[] videoSliceIndexes = videoIndexes.Select(idx => allTasks[idx].OutputIndex).ToArray();
        int[] audioSliceIndexes = audioIndexes.Select(idx => allTasks[idx].OutputIndex).ToArray();
        int[] subSliceIndexes = subIndexes.Select(idx => allTasks[idx].OutputIndex).ToArray();

        int videoCount = videoSliceIndexes.Length;
        int totalCount = bundledIds.Length;

        DecomposedTask[] bundleTasks = containedTaskIndexes.Select(idx => allTasks[idx]).ToArray();

        return new(
            TaskId: $"{groupTag}-bundle-{bundleIndex}",
            ParentJobId: parentJobId,
            GroupTag: groupTag,
            Kind: EncodeTaskKind.Whole,
            OutputIndex: 0,
            Resources: ResolveBundleResources(bundleTasks),
            EstimatedCostUnits: bundleTasks.Sum(task => task.EstimatedCostUnits),
            Label: $"bundle {bundleIndex} ({videoCount} video / {totalCount} total)",
            BundledTaskIds: bundledIds,
            VideoSliceIndexes: videoSliceIndexes,
            AudioSliceIndexes: audioSliceIndexes,
            SubtitleSliceIndexes: subSliceIndexes,
            IncludeThumbnails: thumbsForBundle ? null : false,
            IsPartialTopUp: isPartialTopUp
        );
    }

    /// <summary>
    /// Sum the real resource demand of every task in the bundle. The bundle
    /// will spawn ONE ffmpeg that internally runs all the contained encodes
    /// in parallel via filter_complex, so the GPU/CPU semaphore claim must
    /// reflect the TOTAL hardware footprint — not the per-task max. Without
    /// this, eight per-rung tasks each declaring GpuSlots=1 collapse into a
    /// bundle that only claims one slot from the 8-slot consumer NVIDIA
    /// semaphore — four such bundles would happily run concurrently, asking
    /// for 32 NVENC sessions against an 8-session cap.
    ///
    /// <para>Per-task CpuThreads values double-count (each NVENC task
    /// declares 2 threads of muxing overhead; software-encoder tasks
    /// declare half the box). Naive summing produces a claim larger than
    /// the CPU semaphore — which is sized to the host's core count — and
    /// the bundle waits forever for threads it can never acquire. Cap at
    /// <c>cores - 1</c> so a real-world bundle still leaves headroom for
    /// SignalR / HTTP / other queue work to make progress.</para>
    /// </summary>
    private static ResourceRequirement? ResolveBundleResources(DecomposedTask[] tasks)
    {
        string? gpuKey = tasks
            .Select(task => task.Resources?.GpuDeviceKey)
            .FirstOrDefault(key => key is not null);

        int gpuSlots = tasks.Sum(task => task.Resources?.GpuSlots ?? 0);
        int summedCpuThreads = tasks.Sum(task => task.Resources?.CpuThreads ?? 0);

        int cores = Math.Max(1, Environment.ProcessorCount);
        int cpuCap = Math.Max(1, cores - 1);
        int cpuThreads = Math.Clamp(summedCpuThreads, 1, cpuCap);

        if (gpuKey is null && gpuSlots == 0 && summedCpuThreads == 0)
            return null;

        return new(gpuKey, gpuSlots, cpuThreads);
    }
}
