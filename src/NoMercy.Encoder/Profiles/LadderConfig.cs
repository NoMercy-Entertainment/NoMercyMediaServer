namespace NoMercy.Encoder.Profiles;

public record LadderConfig
{
    public LadderMode Mode { get; init; } = LadderMode.Auto;
    public LadderRung[]? Rungs { get; init; }
    public AutoLadderConfig? AutoConfig { get; init; }
}
