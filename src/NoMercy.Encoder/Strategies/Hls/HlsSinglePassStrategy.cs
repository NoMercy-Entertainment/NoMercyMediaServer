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
/// <para><see cref="Decompose"/> collapses all video rungs into a single
/// video task and all audio outputs into a single audio task. One ffmpeg
/// per kind, with <c>filter_complex split</c> handling every rung from one
/// decode (and one shared HDR→SDR tonemap when the source is HDR). Mixed
/// codecs (HEVC + H.264 fallback) coexist in the same filter graph —
/// each output gets its own <c>-map [vN] -c:v &lt;encoder&gt;</c> block.
/// </para>
///
/// <para>Subtitles, thumbnails, and chapter stills stay fanned out (cheap +
/// independent, easy to fail-in-isolation).</para>
///
/// <para>This caps NVENC session count to the actual rung count of one
/// ffmpeg (not concurrent-processes × rungs) and eliminates the race over
/// shared publish/finalize writes.</para>
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

        // ── Video: one task covering every rung in one filter_complex graph ──
        if (plan.VideoOutputs.Length > 0)
        {
            int[] videoIndexes = Enumerable.Range(0, plan.VideoOutputs.Length).ToArray();
            VideoOutputPlan representative = plan
                .VideoOutputs.OrderByDescending(video => video.Width)
                .First();
            string sizes = string.Join("+", plan.VideoOutputs.Select(video => $"{video.Width}p"));
            string hdr = plan.VideoOutputs.Any(video => video.IsHdrOutput) ? " HDR" : string.Empty;
            string codecs = string.Join(
                "/",
                plan.VideoOutputs.Select(video => video.EncoderName).Distinct()
            );

            tasks.Add(
                new DecomposedTask(
                    TaskId: $"{groupTag}-video-0",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Video,
                    OutputIndex: 0,
                    Resources: TaskResourceHelper.ForVideoOutput(representative),
                    EstimatedCostUnits: plan.VideoOutputs.Sum(EstimateVideoCost),
                    Label: $"{sizes}{hdr} {codecs}",
                    SourceIndexes: videoIndexes
                )
            );
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
