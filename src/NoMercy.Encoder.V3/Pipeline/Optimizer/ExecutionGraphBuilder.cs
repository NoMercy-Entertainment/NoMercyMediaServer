namespace NoMercy.Encoder.V3.Pipeline.Optimizer;

using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Profiles;

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

        // If we have video outputs
        if (media.HasVideo && profile.VideoOutputs.Length > 0)
        {
            // 1. Decode
            string decodeId = $"node_{nodeId++}";
            nodes.Add(
                new ExecutionNode(
                    decodeId,
                    OperationType.Decode,
                    [],
                    new Dictionary<string, string>
                    {
                        ["stream_index"] = "0",
                        ["codec"] = media.VideoStreams[0].Codec,
                    }
                )
            );

            string lastVideoNode = decodeId;

            // 2. Tonemap if any output wants HDR→SDR and source is HDR
            bool needsTonemap =
                media.VideoStreams[0].IsHdr && profile.VideoOutputs.Any(v => v.ConvertHdrToSdr);

            if (needsTonemap)
            {
                string tonemapId = $"node_{nodeId++}";
                nodes.Add(
                    new ExecutionNode(
                        tonemapId,
                        OperationType.Tonemap,
                        [lastVideoNode],
                        new Dictionary<string, string> { ["algorithm"] = "hable" }
                    )
                );
                lastVideoNode = tonemapId;
            }

            // 3. Split if multiple outputs
            if (profile.VideoOutputs.Length > 1)
            {
                string splitId = $"node_{nodeId++}";
                nodes.Add(
                    new ExecutionNode(
                        splitId,
                        OperationType.Split,
                        [lastVideoNode],
                        new Dictionary<string, string>
                        {
                            ["count"] = profile.VideoOutputs.Length.ToString(),
                        }
                    )
                );

                // 4. Scale + Encode per output
                for (int i = 0; i < profile.VideoOutputs.Length; i++)
                {
                    VideoOutput output = profile.VideoOutputs[i];
                    int height =
                        output.Height
                        ?? (
                            output.Width
                            * media.VideoStreams[0].Height
                            / media.VideoStreams[0].Width
                        );

                    string scaleId = $"node_{nodeId++}";
                    nodes.Add(
                        new ExecutionNode(
                            scaleId,
                            OperationType.Scale,
                            [splitId],
                            new Dictionary<string, string>
                            {
                                ["width"] = output.Width.ToString(),
                                ["height"] = height.ToString(),
                                ["split_index"] = i.ToString(),
                            }
                        )
                    );

                    string encodeId = $"node_{nodeId++}";
                    nodes.Add(
                        new ExecutionNode(
                            encodeId,
                            OperationType.Encode,
                            [scaleId],
                            new Dictionary<string, string>
                            {
                                ["encoder"] = resolvedVideoCodecs[i].FfmpegEncoderName,
                                ["crf"] = output.Crf.ToString(),
                                ["preset"] = output.Preset ?? "",
                                ["width"] = output.Width.ToString(),
                                ["height"] = height.ToString(),
                            }
                        )
                    );
                }
            }
            else
            {
                // Single output: scale + encode
                VideoOutput output = profile.VideoOutputs[0];
                int height =
                    output.Height
                    ?? (output.Width * media.VideoStreams[0].Height / media.VideoStreams[0].Width);

                bool needsScale =
                    output.Width != media.VideoStreams[0].Width
                    || height != media.VideoStreams[0].Height;

                if (needsScale)
                {
                    string scaleId = $"node_{nodeId++}";
                    nodes.Add(
                        new ExecutionNode(
                            scaleId,
                            OperationType.Scale,
                            [lastVideoNode],
                            new Dictionary<string, string>
                            {
                                ["width"] = output.Width.ToString(),
                                ["height"] = height.ToString(),
                            }
                        )
                    );
                    lastVideoNode = scaleId;
                }

                string encodeId = $"node_{nodeId++}";
                nodes.Add(
                    new ExecutionNode(
                        encodeId,
                        OperationType.Encode,
                        [lastVideoNode],
                        new Dictionary<string, string>
                        {
                            ["encoder"] = resolvedVideoCodecs[0].FfmpegEncoderName,
                            ["crf"] = output.Crf.ToString(),
                            ["preset"] = output.Preset ?? "",
                            ["width"] = output.Width.ToString(),
                            ["height"] = height.ToString(),
                        }
                    )
                );
            }
        }

        // Audio operations
        for (int i = 0; i < profile.AudioOutputs.Length && i < media.AudioStreams.Count; i++)
        {
            string audioDecodeId = $"node_{nodeId++}";
            nodes.Add(
                new ExecutionNode(
                    audioDecodeId,
                    OperationType.AudioDecode,
                    [],
                    new Dictionary<string, string>
                    {
                        ["stream_index"] = media.AudioStreams[i].Index.ToString(),
                    }
                )
            );

            string audioEncodeId = $"node_{nodeId++}";
            nodes.Add(
                new ExecutionNode(
                    audioEncodeId,
                    OperationType.AudioEncode,
                    [audioDecodeId],
                    new Dictionary<string, string>
                    {
                        ["codec"] = profile.AudioOutputs[i].Codec.ToString(),
                        ["bitrate"] = profile.AudioOutputs[i].BitrateKbps.ToString(),
                        ["channels"] = profile.AudioOutputs[i].Channels.ToString(),
                        ["sample_rate"] = profile.AudioOutputs[i].SampleRateHz.ToString(),
                    }
                )
            );
        }

        // Subtitle extraction (independent operations)
        for (int i = 0; i < profile.SubtitleOutputs.Length && i < media.SubtitleStreams.Count; i++)
        {
            string subExtractId = $"node_{nodeId++}";
            nodes.Add(
                new ExecutionNode(
                    subExtractId,
                    OperationType.SubtitleExtract,
                    [],
                    new Dictionary<string, string>
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
            nodes.Add(
                new ExecutionNode(
                    chapterId,
                    OperationType.ChapterExtract,
                    [],
                    new Dictionary<string, string>()
                )
            );
        }

        // Thumbnail generation (independent)
        if (profile.Thumbnails is not null && media.HasVideo)
        {
            string thumbId = $"node_{nodeId++}";
            nodes.Add(
                new ExecutionNode(
                    thumbId,
                    OperationType.ThumbnailCapture,
                    [],
                    new Dictionary<string, string>
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
