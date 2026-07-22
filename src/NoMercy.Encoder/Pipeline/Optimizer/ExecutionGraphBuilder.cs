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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline.Optimizer;

public class ExecutionGraphBuilder
{
    public List<ExecutionNode> BuildGraph(
        MediaInfo media,
        EncodingProfile profile,
        ResolvedCodec[] resolvedVideoCodecs
    )
    {
        List<ExecutionNode> nodes = [];
        int nodeId = 0;
        // Set when an HDR→SDR tonemap node exists so the thumbnail node can derive
        // from the SDR intermediate instead of sampling raw HDR (crushed colours).
        string? tonemapNodeId = null;

        VideoOutput[] videoOutputs = PlanStageHelpers.EnumerateVideo(profile: profile);

        // If we have video outputs
        if (media.HasVideo && videoOutputs.Length > 0)
        {
            // 1. Decode
            string decodeId = $"node_{nodeId++}";
            nodes.Add(
                item: new(
                    Id: decodeId,
                    Operation: OperationType.Decode,
                    DependsOn: [],
                    Parameters: new() { [key: "stream_index"] = "0", [key: "codec"] = media.VideoStreams[index: 0].Codec }
                )
            );

            string lastVideoNode = decodeId;

            // 2. Tonemap if any output wants HDR→SDR and source is HDR
            bool needsTonemap =
                media.VideoStreams[index: 0].IsHdr && videoOutputs.Any(predicate: v => v.ConvertHdrToSdr);

            if (needsTonemap)
            {
                string tonemapId = $"node_{nodeId++}";
                nodes.Add(
                    item: new(
                        Id: tonemapId,
                        Operation: OperationType.Tonemap,
                        DependsOn: [lastVideoNode],
                        Parameters: new() { [key: "algorithm"] = "hable" }
                    )
                );
                lastVideoNode = tonemapId;
                tonemapNodeId = tonemapId;
            }

            // 3. Split if multiple outputs
            if (videoOutputs.Length > 1)
            {
                string splitId = $"node_{nodeId++}";
                nodes.Add(
                    item: new(
                        Id: splitId,
                        Operation: OperationType.Split,
                        DependsOn: [lastVideoNode],
                        Parameters: new() { [key: "count"] = videoOutputs.Length.ToString() }
                    )
                );

                // 4. Scale + Encode per output
                for (int i = 0; i < videoOutputs.Length; i++)
                {
                    VideoOutput output = videoOutputs[i];
                    // A null (or legacy 0) width means "keep source width".
                    int width = output.Width is int ow and > 0 ? ow : media.VideoStreams[index: 0].Width;
                    int height =
                        output.Height
                        ?? (width * media.VideoStreams[index: 0].Height / media.VideoStreams[index: 0].Width);

                    string scaleId = $"node_{nodeId++}";
                    nodes.Add(
                        item: new(
                            Id: scaleId,
                            Operation: OperationType.Scale,
                            DependsOn: [splitId],
                            Parameters: new()
                            {
                                [key: "width"] = width.ToString(),
                                [key: "height"] = height.ToString(),
                                [key: "split_index"] = i.ToString(),
                            }
                        )
                    );

                    string encodeId = $"node_{nodeId++}";
                    nodes.Add(
                        item: new(
                            Id: encodeId,
                            Operation: OperationType.Encode,
                            DependsOn: [scaleId],
                            Parameters: new()
                            {
                                [key: "encoder"] = resolvedVideoCodecs[i].FfmpegEncoderName,
                                [key: "crf"] = output.Crf.ToString(),
                                [key: "preset"] = output.Preset ?? "",
                                [key: "width"] = width.ToString(),
                                [key: "height"] = height.ToString(),
                            }
                        )
                    );
                }
            }
            else
            {
                // Single output: scale + encode
                VideoOutput output = videoOutputs[0];
                // A null (or legacy 0) width means "keep source width".
                int width = output.Width is int ow and > 0 ? ow : media.VideoStreams[index: 0].Width;
                int height =
                    output.Height
                    ?? (width * media.VideoStreams[index: 0].Height / media.VideoStreams[index: 0].Width);

                bool needsScale =
                    width != media.VideoStreams[index: 0].Width || height != media.VideoStreams[index: 0].Height;

                if (needsScale)
                {
                    string scaleId = $"node_{nodeId++}";
                    nodes.Add(
                        item: new(
                            Id: scaleId,
                            Operation: OperationType.Scale,
                            DependsOn: [lastVideoNode],
                            Parameters: new() { [key: "width"] = width.ToString(), [key: "height"] = height.ToString() }
                        )
                    );
                    lastVideoNode = scaleId;
                }

                string encodeId = $"node_{nodeId++}";
                nodes.Add(
                    item: new(
                        Id: encodeId,
                        Operation: OperationType.Encode,
                        DependsOn: [lastVideoNode],
                        Parameters: new()
                        {
                            [key: "encoder"] = resolvedVideoCodecs[0].FfmpegEncoderName,
                            [key: "crf"] = output.Crf.ToString(),
                            [key: "preset"] = output.Preset ?? "",
                            [key: "width"] = width.ToString(),
                            [key: "height"] = height.ToString(),
                        }
                    )
                );
            }
        }

        // Audio operations
        for (int i = 0; i < profile.Audio.Length && i < media.AudioStreams.Count; i++)
        {
            string audioDecodeId = $"node_{nodeId++}";
            nodes.Add(
                item: new(
                    Id: audioDecodeId,
                    Operation: OperationType.AudioDecode,
                    DependsOn: [],
                    Parameters: new() { [key: "stream_index"] = media.AudioStreams[index: i].Index.ToString() }
                )
            );

            string audioEncodeId = $"node_{nodeId++}";
            nodes.Add(
                item: new(
                    Id: audioEncodeId,
                    Operation: OperationType.AudioEncode,
                    DependsOn: [audioDecodeId],
                    Parameters: new()
                    {
                        [key: "codec"] = profile.Audio[i].Codec.ToString(),
                        [key: "bitrate"] = profile.Audio[i].BitrateKbps.ToString(),
                        [key: "channels"] = profile.Audio[i].Channels.ToString(),
                        [key: "sample_rate"] = profile.Audio[i].SampleRateHz.ToString(),
                    }
                )
            );
        }

        // Subtitle extraction (independent operations)
        for (int i = 0; i < profile.Subtitles.Length && i < media.SubtitleStreams.Count; i++)
        {
            string subExtractId = $"node_{nodeId++}";
            nodes.Add(
                item: new(
                    Id: subExtractId,
                    Operation: OperationType.SubtitleExtract,
                    DependsOn: [],
                    Parameters: new()
                    {
                        [key: "stream_index"] = media.SubtitleStreams[index: i].Index.ToString(),
                        [key: "language"] = media.SubtitleStreams[index: i].Language ?? "und",
                    }
                )
            );
        }

        // Chapter extraction (independent)
        if (media.Chapters.Count > 0)
        {
            string chapterId = $"node_{nodeId++}";
            nodes.Add(item: new(Id: chapterId, Operation: OperationType.ChapterExtract, DependsOn: [], Parameters: new()));
        }

        // Thumbnail generation (independent)
        if (profile.Thumbnails is not null && media.HasVideo)
        {
            string thumbId = $"node_{nodeId++}";
            string[] thumbDeps = tonemapNodeId is not null ? [tonemapNodeId] : [];
            nodes.Add(
                item: new(
                    Id: thumbId,
                    Operation: OperationType.ThumbnailCapture,
                    DependsOn: thumbDeps,
                    Parameters: new()
                    {
                        [key: "width"] = profile.Thumbnails.Width.ToString(),
                        [key: "interval"] = profile.Thumbnails.IntervalSeconds.ToString(),
                    }
                )
            );
        }

        return nodes;
    }
}
