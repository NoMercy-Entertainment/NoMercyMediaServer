namespace NoMercy.Encoder.V3.SystemFeatures;

public interface IEncoderPlugin
{
    string Name { get; }

    string Version { get; }

    void OnPipelineStageStart(string stageName, object context);

    void OnPipelineStageEnd(string stageName, object context, object result);
}

public enum PipelineHook
{
    BeforeAnalyze,
    AfterAnalyze,
    BeforeBuild,
    AfterBuild,
    BeforeExecute,
    AfterExecute,
    BeforeFinalize,
    AfterFinalize,
}
