namespace NoMercy.Encoder.Profiles;

/// <summary>
/// A single rung in a manually-specified adaptive bitrate ladder.
/// Used when <see cref="EncodingProfile.AutoLadder"/> is false and explicit rungs are needed.
/// </summary>
public record LadderRung(int Width, int Height, int BitrateKbps);
