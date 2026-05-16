using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Shared;

namespace NoMercy.Encoder.Strategies.Hls;

/// <summary>
/// HLS single-pass strategy. Delegates to the shared 6-stage
/// <see cref="IEncoder"/> pipeline for single-task execution.
///
/// <para><see cref="Decompose"/> groups variants by (codec × hardware bucket)
/// so each emitted task is ONE ffmpeg process running filter_complex split
/// over the rungs in its bucket — one HDR→SDR tonemap per bucket, one source
/// read per bucket, one batched output. Buckets:
/// <list type="bullet">
///   <item><b>VideoGroup-&lt;encoder&gt;</b> — all video rungs sharing the same
///   encoder name (e.g. all <c>hevc_nvenc</c>, all <c>libx264</c>) → one
///   filter_complex split-scale-encode chain.</item>
///   <item><b>AudioGroup</b> — all audio outputs in one ffmpeg.</item>
///   <item><b>Subtitle / Thumbnails / Chapters</b> — light tasks, one each.</item>
/// </list>
/// This caps concurrent NVENC sessions to the rung-count of the heaviest
/// bucket (not total processes × rungs) and avoids racing shared
/// publish/finalize writes, while still letting CPU and GPU buckets run in
/// parallel.</para>
/// </summary>
public class HlsSinglePassStrategy(IEncoder encoder) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.Hls;
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    ) => encoder.EncodeAsync(request, progress, ct);

    public DecomposedTask[] Decompose(OutputPlan plan, string groupTag)
    {
        List<DecomposedTask> tasks = [];

        // ── Video: one task per encoder bucket (filter_complex split inside) ──
        IEnumerable<IGrouping<string, (VideoOutputPlan video, int sourceIndex)>> videoGroups = plan
            .VideoOutputs.Select((video, index) => (video, sourceIndex: index))
            .GroupBy(entry => entry.video.EncoderName);

        int bucketIndex = 0;
        foreach (IGrouping<string, (VideoOutputPlan video, int sourceIndex)> bucket in videoGroups)
        {
            (VideoOutputPlan video, int sourceIndex)[] rungs = bucket.ToArray();
            int[] sourceIndexes = rungs.Select(rung => rung.sourceIndex).ToArray();
            VideoOutputPlan representative = rungs[0].video;
            string sizes = string.Join("+", rungs.Select(rung => $"{rung.video.Width}p"));
            string hdr = rungs.Any(rung => rung.video.IsHdrOutput) ? " HDR" : string.Empty;

            tasks.Add(
                new DecomposedTask(
                    TaskId: $"{groupTag}-video-{bucketIndex}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Video,
                    OutputIndex: bucketIndex,
                    Resources: TaskResourceHelper.ForVideoOutput(representative),
                    EstimatedCostUnits: rungs.Sum(rung => EstimateVideoCost(rung.video)),
                    Label: $"{sizes}{hdr} {bucket.Key}",
                    SourceIndexes: sourceIndexes
                )
            );
            bucketIndex++;
        }

        // ── Audio: one task covering all audio outputs ────────────────────────
        if (plan.AudioOutputs.Length > 0)
        {
            int[] audioIndexes = Enumerable.Range(0, plan.AudioOutputs.Length).ToArray();
            string label =
                plan.AudioOutputs.Length == 1
                    ? $"{plan.AudioOutputs[0].Language ?? "und"} {plan.AudioOutputs[0].EncoderName}"
                    : $"{plan.AudioOutputs.Length} tracks";

            tasks.Add(
                new DecomposedTask(
                    TaskId: $"{groupTag}-audio-0",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Audio,
                    OutputIndex: 0,
                    Resources: TaskResourceHelper.CpuOnly(2),
                    EstimatedCostUnits: plan.AudioOutputs.Length,
                    Label: label,
                    SourceIndexes: audioIndexes
                )
            );
        }

        for (int i = 0; i < plan.SubtitleOutputs.Length; i++)
        {
            SubtitleOutputPlan sub = plan.SubtitleOutputs[i];
            string lang = sub.Language ?? "und";

            tasks.Add(
                new DecomposedTask(
                    TaskId: $"{groupTag}-sub-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Subtitle,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.CpuOnly(1),
                    EstimatedCostUnits: 1,
                    Label: $"sub {lang} {sub.OutputCodec}"
                )
            );
        }

        if (plan.Thumbnails is not null)
        {
            tasks.Add(
                new DecomposedTask(
                    TaskId: $"{groupTag}-thumbs",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Thumbnails,
                    OutputIndex: 0,
                    Resources: TaskResourceHelper.CpuOnly(1),
                    EstimatedCostUnits: 1,
                    Label: $"thumbnails {plan.Thumbnails.Width}x{plan.Thumbnails.Height}"
                )
            );
        }

        if (plan.GenerateChapterThumbs && plan.Chapters is { Count: > 0 })
        {
            int count = plan.Chapters.Count;
            for (int i = 0; i < count; i++)
            {
                ChapterInfo chapter = plan.Chapters[i];
                tasks.Add(
                    new DecomposedTask(
                        TaskId: $"{groupTag}-chapter-{i}",
                        ParentJobId: 0,
                        GroupTag: groupTag,
                        Kind: EncodeTaskKind.Chapters,
                        OutputIndex: i,
                        Resources: TaskResourceHelper.CpuOnly(1),
                        EstimatedCostUnits: 1,
                        Label: $"chapter still {i + 1}/{count} @ {chapter.Start.TotalSeconds:F0}s"
                    )
                );
            }
        }

        if (tasks.Count == 0)
            return [IEncodingStrategy.WholeTask(groupTag)];

        return tasks.ToArray();
    }

    private static int EstimateVideoCost(VideoOutputPlan video)
    {
        int cost = 1;

        if (video.Width >= 3840)
            cost = 8;
        else if (video.Width >= 1920)
            cost = 4;
        else if (video.Width >= 1280)
            cost = 2;

        if (video.ConvertHdrToSdr)
            cost++;

        return cost;
    }
}
