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
/// <para><see cref="Decompose"/> emits one <see cref="DecomposedTask"/> per
/// stream-unit of work: one Video per ladder rung, one Audio per mix group,
/// one Subtitle per text track, one Thumbnails, one Chapter still. This is
/// the model the rest of the pipeline tracks against — per-stream outcome
/// rows, per-stream retry, per-stream dashboard progress.
/// </para>
///
/// <para><b>Smart-combine is NOT done here.</b> The coordinator
/// (<c>VideoEncodeJob.DispatchDecomposedAsync</c>) inspects worker capacity
/// at dispatch time and packs N stream-tasks into one <c>EncodeTaskJob</c>
/// covering all of them. That job runs ONE ffmpeg with filter_complex
/// multi-output (each contained task keeps its own -map / filter chain)
/// and writes per-stream outcome rows. The decomposition layer stays
/// per-stream so the bundler has the granularity it needs.</para>
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

        for (int i = 0; i < plan.VideoOutputs.Length; i++)
        {
            VideoOutputPlan video = plan.VideoOutputs[i];
            string resolution = $"{video.Width}p";
            string hdr = video.IsHdrOutput ? " HDR" : string.Empty;

            tasks.Add(
                new DecomposedTask(
                    TaskId: $"{groupTag}-video-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Video,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.ForVideoOutput(video),
                    EstimatedCostUnits: EstimateVideoCost(video),
                    Label: $"{resolution}{hdr} {video.EncoderName}",
                    VideoWidth: video.Width,
                    VideoEncoderName: video.EncoderName
                )
            );
        }

        for (int i = 0; i < plan.AudioOutputs.Length; i++)
        {
            AudioOutputPlan audio = plan.AudioOutputs[i];
            string lang = audio.Language ?? "und";

            tasks.Add(
                new DecomposedTask(
                    TaskId: $"{groupTag}-audio-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Audio,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.CpuOnly(1),
                    EstimatedCostUnits: 1,
                    Label: $"{lang} {audio.EncoderName} {audio.Channels}ch"
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
