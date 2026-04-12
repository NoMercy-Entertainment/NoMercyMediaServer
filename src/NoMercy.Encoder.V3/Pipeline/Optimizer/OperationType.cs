namespace NoMercy.Encoder.V3.Pipeline.Optimizer;

public enum OperationType
{
    Decode,
    HwUpload,
    HwDownload,
    Tonemap,
    Deinterlace,
    Scale,
    Crop,
    Split,
    Encode,
    AudioDecode,
    AudioEncode,
    AudioResample,
    SubtitleExtract,
    SubtitleConvert,
    SubtitleBurnIn,
    SubtitleOcr,
    ThumbnailCapture,
    SpriteAssemble,
    ChapterExtract,
    FontExtract,
    Mux,
}
