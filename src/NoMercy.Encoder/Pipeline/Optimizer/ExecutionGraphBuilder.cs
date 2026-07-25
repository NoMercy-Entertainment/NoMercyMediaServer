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

        VideoOutput[] videoOutputs = PlanStageHelpers.EnumerateVideo(profile);

        // If we have video outputs
        if (media.HasVideo && videoOutputs.Length > 0)
        {
            // 1. Decode
            string decodeId = $"node_{nodeId++}";
            nodes.Add(
                new(
                    decodeId,
                    OperationType.Decode,
                    [],
                    new() { ["stream_index"] = "0", ["codec"] = media.VideoStreams[0].Codec }
                )
            );

            string lastVideoNode = decodeId;

            // 2. Tonemap if any output wants HDR→SDR and source is HDR
            bool needsTonemap =
                media.VideoStreams[0].IsHdr && videoOutputs.Any(v => v.ConvertHdrToSdr);

            if (needsTonemap)
            {
                string tonemapId = $"node_{nodeId++}";
                nodes.Add(
                    new(
                        tonemapId,
                        OperationType.Tonemap,
                        [lastVideoNode],
                        new() { ["algorithm"] = "hable" }
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
                    new(
                        splitId,
                        OperationType.Split,
                        [lastVideoNode],
                        new() { ["count"] = videoOutputs.Length.ToString() }
                    )
                );

                // 4. Scale + Encode per output
                for (int i = 0; i < videoOutputs.Length; i++)
                {
                    VideoOutput output = videoOutputs[i];
                    // A null (or legacy 0) width means "keep source width".
                    int width = output.Width is int ow and > 0 ? ow : media.VideoStreams[0].Width;
                    int height =
                        output.Height
                        ?? (width * media.VideoStreams[0].Height / media.VideoStreams[0].Width);

                    string scaleId = $"node_{nodeId++}";
                    nodes.Add(
                        new(
                            scaleId,
                            OperationType.Scale,
                            [splitId],
                            new()
                            {
                                ["width"] = width.ToString(),
                                ["height"] = height.ToString(),
                                ["split_index"] = i.ToString(),
                            }
                        )
                    );

                    string encodeId = $"node_{nodeId++}";
                    nodes.Add(
                        new(
                            encodeId,
                            OperationType.Encode,
                            [scaleId],
                            new()
                            {
                                ["encoder"] = resolvedVideoCodecs[i].FfmpegEncoderName,
                                ["crf"] = output.Crf.ToString(),
                                ["preset"] = output.Preset ?? "",
                                ["width"] = width.ToString(),
                                ["height"] = height.ToString(),
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
                int width = output.Width is int ow and > 0 ? ow : media.VideoStreams[0].Width;
                int height =
                    output.Height
                    ?? (width * media.VideoStreams[0].Height / media.VideoStreams[0].Width);

                bool needsScale =
                    width != media.VideoStreams[0].Width || height != media.VideoStreams[0].Height;

                if (needsScale)
                {
                    string scaleId = $"node_{nodeId++}";
                    nodes.Add(
                        new(
                            scaleId,
                            OperationType.Scale,
                            [lastVideoNode],
                            new() { ["width"] = width.ToString(), ["height"] = height.ToString() }
                        )
                    );
                    lastVideoNode = scaleId;
                }

                string encodeId = $"node_{nodeId++}";
                nodes.Add(
                    new(
                        encodeId,
                        OperationType.Encode,
                        [lastVideoNode],
                        new()
                        {
                            ["encoder"] = resolvedVideoCodecs[0].FfmpegEncoderName,
                            ["crf"] = output.Crf.ToString(),
                            ["preset"] = output.Preset ?? "",
                            ["width"] = width.ToString(),
                            ["height"] = height.ToString(),
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
                new(
                    audioDecodeId,
                    OperationType.AudioDecode,
                    [],
                    new() { ["stream_index"] = media.AudioStreams[i].Index.ToString() }
                )
            );

            string audioEncodeId = $"node_{nodeId++}";
            nodes.Add(
                new(
                    audioEncodeId,
                    OperationType.AudioEncode,
                    [audioDecodeId],
                    new()
                    {
                        ["codec"] = profile.Audio[i].Codec.ToString(),
                        ["bitrate"] = profile.Audio[i].BitrateKbps.ToString(),
                        ["channels"] = profile.Audio[i].Channels.ToString(),
                        ["sample_rate"] = profile.Audio[i].SampleRateHz.ToString(),
                    }
                )
            );
        }

        // Subtitle extraction (independent operations)
        for (int i = 0; i < profile.Subtitles.Length && i < media.SubtitleStreams.Count; i++)
        {
            string subExtractId = $"node_{nodeId++}";
            nodes.Add(
                new(
                    subExtractId,
                    OperationType.SubtitleExtract,
                    [],
                    new()
                    {
                        ["stream_index"] = media.SubtitleStreams[i].Index.ToString(),
                        ["language"] = media.SubtitleStreams[i].Language ?? "und",
                    }
                )
            );
        }

        // Chapter extraction (independent)
        if (media.Chapters.Count > 0)
        {
            string chapterId = $"node_{nodeId++}";
            nodes.Add(new(chapterId, OperationType.ChapterExtract, [], new()));
        }

        // Thumbnail generation (independent)
        if (profile.Thumbnails is not null && media.HasVideo)
        {
            string thumbId = $"node_{nodeId++}";
            string[] thumbDeps = tonemapNodeId is not null ? [tonemapNodeId] : [];
            nodes.Add(
                new(
                    thumbId,
                    OperationType.ThumbnailCapture,
                    thumbDeps,
                    new()
                    {
                        ["width"] = profile.Thumbnails.Width.ToString(),
                        ["interval"] = profile.Thumbnails.IntervalSeconds.ToString(),
                    }
                )
            );
        }

        return nodes;
    }
}
