namespace NoMercy.Encoder.V3.SystemFeatures;

using NoMercy.Encoder.V3.Pipeline;

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
