using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Codecs.Subtitle;

/// <summary>
/// Base class for subtitle codecs
/// </summary>
public abstract class SubtitleCodecBase : ISubtitleCodec
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public CodecType Type => CodecType.Subtitle;
    public virtual bool RequiresHardwareAcceleration => false;
    public virtual HardwareAcceleration? HardwareAccelerationType => null;

    public bool BurnIn { get; set; }

    public virtual IReadOnlyList<string> BuildArguments()
    {
        return ["-c:s", Name];
    }

    public virtual ValidationResult Validate()
    {
        return ValidationResult.Success();
    }

    public abstract ISubtitleCodec Clone();
}

/// <summary>
/// WebVTT subtitle codec
/// </summary>
public sealed class WebvttCodec : SubtitleCodecBase
{
    public override string Name => "webvtt";
    public override string DisplayName => "WebVTT";

    public override ISubtitleCodec Clone() => new WebvttCodec { BurnIn = BurnIn };
}

/// <summary>
/// SRT subtitle codec
/// </summary>
public sealed class SrtCodec : SubtitleCodecBase
{
    public override string Name => "srt";
    public override string DisplayName => "SRT";

    public override ISubtitleCodec Clone() => new SrtCodec { BurnIn = BurnIn };
}

/// <summary>
/// ASS/SSA subtitle codec
/// </summary>
public sealed class AssCodec : SubtitleCodecBase
{
    public override string Name => "ass";
    public override string DisplayName => "ASS/SSA";

    public override ISubtitleCodec Clone() => new AssCodec { BurnIn = BurnIn };
}

/// <summary>
/// MOV text subtitle codec (for MP4 containers)
/// </summary>
public sealed class MovTextCodec : SubtitleCodecBase
{
    public override string Name => "mov_text";
    public override string DisplayName => "MOV Text";

    public override ISubtitleCodec Clone() => new MovTextCodec { BurnIn = BurnIn };
}

/// <summary>
/// DVB subtitle codec
/// </summary>
public sealed class DvbSubCodec : SubtitleCodecBase
{
    public override string Name => "dvbsub";
    public override string DisplayName => "DVB Subtitle";

    public override ISubtitleCodec Clone() => new DvbSubCodec { BurnIn = BurnIn };
}

/// <summary>
/// Copy subtitle codec (stream copy)
/// </summary>
public sealed class SubtitleCopyCodec : ISubtitleCodec
{
    public string Name => "copy";
    public string DisplayName => "Copy (No Re-encoding)";
    public CodecType Type => CodecType.Subtitle;
    public bool RequiresHardwareAcceleration => false;
    public HardwareAcceleration? HardwareAccelerationType => null;
    public bool BurnIn { get; set; }

    public IReadOnlyList<string> BuildArguments() => ["-c:s", "copy"];

    public ValidationResult Validate() => ValidationResult.Success();

    public ISubtitleCodec Clone() => new SubtitleCopyCodec { BurnIn = BurnIn };
}
