namespace NoMercy.EncoderV2.Shared.Dtos;

public record EncoderCapability
{
    public string Name { get; set; } = string.Empty;
    public string LongName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // 'V' = video, 'A' = audio, 'S' = subtitle, 'I' = image
    public bool IsHardware { get; set; }
    public Dictionary<string, EncoderOption> Options { get; set; } = [];
}

public record EncoderOption
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // 'bool', 'int', 'uint64', 'int64', 'float', 'double', 'string', 'rational'
    public string? Default { get; set; }
    public object? Min { get; set; }
    public object? Max { get; set; }
    public List<string> Choices { get; set; } = [];
    public string Help { get; set; } = string.Empty;
    public bool Deprecated { get; set; }
}

public record ContainerCapability
{
    public string Name { get; set; } = string.Empty;
    public string LongName { get; set; } = string.Empty;
    public bool CanMux { get; set; }
    public bool CanDemux { get; set; }
    public List<string> SupportedVideoCodecs { get; set; } = [];
    public List<string> SupportedAudioCodecs { get; set; } = [];
    public List<string> SupportedSubtitleCodecs { get; set; } = [];
}

public record AllCapabilities
{
    public Dictionary<string, EncoderCapability> VideoEncoders { get; set; } = [];
    public Dictionary<string, EncoderCapability> AudioEncoders { get; set; } = [];
    public Dictionary<string, EncoderCapability> SubtitleEncoders { get; set; } = [];
    public Dictionary<string, ContainerCapability> Containers { get; set; } = [];
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
