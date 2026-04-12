namespace NoMercy.Encoder.V3.Composition;

public record EncoderOptions(string FfmpegPath, string FfprobePath, bool AutoCalibrate = true);
