using NoMercy.Encoder.Pipeline;

namespace NoMercy.Encoder.SystemFeatures;

public interface IEncoderPlugin
{
    string Name { get; }

    string Version { get; }

    PipelineHook[] GetHooks();
}

public record PipelineHook(
    PipelineStagePosition Position,
    string TargetStage,
    IPipelineStage Stage
);

public enum PipelineStagePosition
{
    Before,
    After,
    Replace,
}
