using Microsoft.Extensions.DependencyInjection;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class StageInterfaceTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNoMercyEncoder(opts =>
        {
            opts.FfmpegPathOverride = "ffmpeg";
            opts.FfprobePathOverride = "ffprobe";
        });
        return services.BuildServiceProvider();
    }

    // ── DI resolution via role interface ─────────────────────────────────────

    [Fact]
    public void IValidationStage_ResolvesAsNonNull()
    {
        ServiceProvider provider = BuildProvider();
        IValidationStage stage = provider.GetRequiredService<IValidationStage>();
        stage.Should().NotBeNull();
    }

    [Fact]
    public void IValidationStage_ResolvesAsSameTypeAsConcreteStage()
    {
        ServiceProvider provider = BuildProvider();
        IValidationStage byInterface = provider.GetRequiredService<IValidationStage>();
        ValidateStage byConcrete = provider.GetRequiredService<ValidateStage>();
        byInterface.Should().BeOfType(byConcrete.GetType());
    }

    [Fact]
    public void IAnalysisStage_ResolvesAsNonNull()
    {
        ServiceProvider provider = BuildProvider();
        IAnalysisStage stage = provider.GetRequiredService<IAnalysisStage>();
        stage.Should().NotBeNull();
    }

    [Fact]
    public void IAnalysisStage_ResolvesAsSameTypeAsConcreteStage()
    {
        ServiceProvider provider = BuildProvider();
        IAnalysisStage byInterface = provider.GetRequiredService<IAnalysisStage>();
        AnalyzeStage byConcrete = provider.GetRequiredService<AnalyzeStage>();
        byInterface.Should().BeOfType(byConcrete.GetType());
    }

    [Fact]
    public void IPlanStage_ResolvesAsNonNull()
    {
        ServiceProvider provider = BuildProvider();
        IPlanStage stage = provider.GetRequiredService<IPlanStage>();
        stage.Should().NotBeNull();
    }

    [Fact]
    public void IPlanStage_ResolvesAsSameTypeAsConcreteStage()
    {
        ServiceProvider provider = BuildProvider();
        IPlanStage byInterface = provider.GetRequiredService<IPlanStage>();
        PlanStage byConcrete = provider.GetRequiredService<PlanStage>();
        byInterface.Should().BeOfType(byConcrete.GetType());
    }

    [Fact]
    public void IBuildStage_ResolvesAsNonNull()
    {
        ServiceProvider provider = BuildProvider();
        IBuildStage stage = provider.GetRequiredService<IBuildStage>();
        stage.Should().NotBeNull();
    }

    [Fact]
    public void IBuildStage_ResolvesAsSameTypeAsConcreteStage()
    {
        ServiceProvider provider = BuildProvider();
        IBuildStage byInterface = provider.GetRequiredService<IBuildStage>();
        BuildStage byConcrete = provider.GetRequiredService<BuildStage>();
        byInterface.Should().BeOfType(byConcrete.GetType());
    }

    [Fact]
    public void IExecutionStage_ResolvesAsNonNull()
    {
        ServiceProvider provider = BuildProvider();
        IExecutionStage stage = provider.GetRequiredService<IExecutionStage>();
        stage.Should().NotBeNull();
    }

    [Fact]
    public void IExecutionStage_ResolvesAsSameTypeAsConcreteStage()
    {
        ServiceProvider provider = BuildProvider();
        IExecutionStage byInterface = provider.GetRequiredService<IExecutionStage>();
        ExecuteStage byConcrete = provider.GetRequiredService<ExecuteStage>();
        byInterface.Should().BeOfType(byConcrete.GetType());
    }

    [Fact]
    public void IFinalizeStage_ResolvesAsNonNull()
    {
        ServiceProvider provider = BuildProvider();
        IFinalizeStage stage = provider.GetRequiredService<IFinalizeStage>();
        stage.Should().NotBeNull();
    }

    [Fact]
    public void IFinalizeStage_ResolvesAsSameTypeAsConcreteStage()
    {
        ServiceProvider provider = BuildProvider();
        IFinalizeStage byInterface = provider.GetRequiredService<IFinalizeStage>();
        FinalizeStage byConcrete = provider.GetRequiredService<FinalizeStage>();
        byInterface.Should().BeOfType(byConcrete.GetType());
    }

    // ── Concrete stages carry the interface ──────────────────────────────────

    [Fact]
    public void ValidateStage_ImplementsIValidationStage()
    {
        ServiceProvider provider = BuildProvider();
        ValidateStage stage = provider.GetRequiredService<ValidateStage>();
        stage.Should().BeAssignableTo<IValidationStage>();
    }

    [Fact]
    public void AnalyzeStage_ImplementsIAnalysisStage()
    {
        ServiceProvider provider = BuildProvider();
        AnalyzeStage stage = provider.GetRequiredService<AnalyzeStage>();
        stage.Should().BeAssignableTo<IAnalysisStage>();
    }

    [Fact]
    public void PlanStage_ImplementsIPlanStage()
    {
        ServiceProvider provider = BuildProvider();
        PlanStage stage = provider.GetRequiredService<PlanStage>();
        stage.Should().BeAssignableTo<IPlanStage>();
    }

    [Fact]
    public void BuildStage_ImplementsIBuildStage()
    {
        ServiceProvider provider = BuildProvider();
        BuildStage stage = provider.GetRequiredService<BuildStage>();
        stage.Should().BeAssignableTo<IBuildStage>();
    }

    [Fact]
    public void ExecuteStage_ImplementsIExecutionStage()
    {
        ServiceProvider provider = BuildProvider();
        ExecuteStage stage = provider.GetRequiredService<ExecuteStage>();
        stage.Should().BeAssignableTo<IExecutionStage>();
    }

    [Fact]
    public void FinalizeStage_ImplementsIFinalizeStage()
    {
        ServiceProvider provider = BuildProvider();
        FinalizeStage stage = provider.GetRequiredService<FinalizeStage>();
        stage.Should().BeAssignableTo<IFinalizeStage>();
    }

    // ── Strategy override hook ────────────────────────────────────────────────

    [Fact]
    public void IEncodingStrategy_ExtendsIStageOverrides()
    {
        // Structural check — no runtime instantiation needed.
        typeof(IEncodingStrategy).Should().Implement<IStageOverrides>();
    }

    [Fact]
    public void IStageOverrides_DefaultsAllNullForStrategyWithNoOverride()
    {
        // A strategy that doesn't override anything should return null for every role.
        // Default interface members are only accessible via the interface type.
        IStageOverrides strategy = new NoOpStrategy();
        strategy.Validation.Should().BeNull();
        strategy.Analysis.Should().BeNull();
        strategy.Plan.Should().BeNull();
        strategy.Build.Should().BeNull();
        strategy.Execution.Should().BeNull();
        strategy.Finalize.Should().BeNull();
    }

    [Fact]
    public void IStageOverrides_CustomPlanOverride_IsObservableThroughInterface()
    {
        StubPlanStage stub = new();
        StrategyWithPlanOverride strategy = new(stub);

        IStageOverrides overrides = strategy;
        overrides.Plan.Should().BeSameAs(stub);
        overrides.Validation.Should().BeNull();
        overrides.Analysis.Should().BeNull();
        overrides.Build.Should().BeNull();
        overrides.Execution.Should().BeNull();
        overrides.Finalize.Should().BeNull();
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Minimal strategy that accepts all IEncodingStrategy defaults.</summary>
    private sealed class NoOpStrategy : IEncodingStrategy
    {
        public OutputFormat Format => OutputFormat.Hls;
        public EncodeMode EncodeMode => EncodeMode.SinglePass;

        public Task<EncodingResult> EncodeAsync(
            EncodingRequest request,
            IProgressObserver? progress,
            CancellationToken ct
        ) => Task.FromResult(new EncodingResult(false, string.Empty, TimeSpan.Zero, null, null!));
    }

    /// <summary>Strategy that overrides only the Plan stage.</summary>
    private sealed class StrategyWithPlanOverride(IPlanStage planOverride) : IEncodingStrategy
    {
        public OutputFormat Format => OutputFormat.Hls;
        public EncodeMode EncodeMode => EncodeMode.SinglePass;
        public IPlanStage? Plan => planOverride;

        public Task<EncodingResult> EncodeAsync(
            EncodingRequest request,
            IProgressObserver? progress,
            CancellationToken ct
        ) => Task.FromResult(new EncodingResult(false, string.Empty, TimeSpan.Zero, null, null!));
    }

    /// <summary>Stub IPlanStage that satisfies the type contract without any logic.</summary>
    private sealed class StubPlanStage : IPlanStage
    {
        public string Name => "StubPlan";

        public Task<StageResult> ExecuteAsync(
            ValidateInput input,
            EncodingContext context,
            CancellationToken ct
        ) =>
            Task.FromResult<StageResult>(
                new StageFailure(new(EncodingErrorKind.Unknown, "stub", null, Name, false))
            );
    }
}
