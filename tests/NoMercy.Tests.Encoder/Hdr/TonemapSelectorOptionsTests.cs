using Moq;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.Hdr;

using HdrOptions = NoMercy.Encoder.Profiles.HdrOptions;

/// <summary>
/// Tests for TonemapSelector.Build() — per-profile algorithm + LUT resolution.
/// Precedence chain: HdrOptions.TonemapAlgorithm → profile.TonemapAlgorithm → "hable".
/// </summary>
public class TonemapSelectorOptionsTests
{
    private readonly TonemapSelector _selector = new();
    private readonly ScopedDecisionLog _decisions = new();

    // -------------------------------------------------------------------------
    // Algorithm precedence
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_defaults_to_hable_at_100_nits_when_no_options()
    {
        TonemapPlan plan = _selector.Build(null, null, _decisions);

        plan.Algorithm.Should().Be("hable");
        plan.PeakNits.Should().Be(100);
        plan.LutFilterChain.Should().BeNull();
        plan.FilterStringFragment.Should().Contain("tonemap=hable");
        plan.FilterStringFragment.Should().Contain("npl=100");
    }

    [Fact]
    public void Build_uses_HdrOptions_TonemapAlgorithm_when_set()
    {
        HdrOptions options = new("mobius", 100);

        TonemapPlan plan = _selector.Build(options, null, _decisions);

        plan.Algorithm.Should().Be("mobius");
        plan.FilterStringFragment.Should().Contain("tonemap=mobius");
    }

    [Fact]
    public void Build_uses_profile_TonemapAlgorithm_when_HdrOptions_null()
    {
        TonemapPlan plan = _selector.Build(null, "reinhard", _decisions);

        plan.Algorithm.Should().Be("reinhard");
        plan.FilterStringFragment.Should().Contain("tonemap=reinhard");
    }

    [Fact]
    public void Build_HdrOptions_wins_over_profile_TonemapAlgorithm()
    {
        HdrOptions options = new("clip", 100);

        TonemapPlan plan = _selector.Build(options, "reinhard", _decisions);

        plan.Algorithm.Should().Be("clip");
        plan.FilterStringFragment.Should().Contain("tonemap=clip");
        plan.FilterStringFragment.Should().NotContain("reinhard");
    }

    [Fact]
    public void Build_unknown_algorithm_falls_back_to_hable_with_decision()
    {
        HdrOptions options = new("made-up", 100);

        TonemapPlan plan = _selector.Build(options, null, _decisions);

        plan.Algorithm.Should().Be("hable");
        plan.FilterStringFragment.Should().Contain("tonemap=hable");

        IReadOnlyList<DecisionLog> snapshot = _decisions.Snapshot();
        snapshot.Should().Contain(d => d.Key == "plan.tonemap_unknown_algorithm_defaulted");
    }

    // -------------------------------------------------------------------------
    // Peak nits
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_TonemapPeakNits_passes_through_to_npl_param()
    {
        HdrOptions options = new("hable", 1000);

        TonemapPlan plan = _selector.Build(options, null, _decisions);

        plan.PeakNits.Should().Be(1000);
        plan.FilterStringFragment.Should().Contain("npl=1000");
    }

    // -------------------------------------------------------------------------
    // LUT path — IStorage mock
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_LutPath_set_returns_lut3d_filter_and_skips_tonemap()
    {
        string lutPath = "C:/luts/film.cube";
        HdrOptions options = new("hable", 100, lutPath);

        Mock<IStorage> storage = new();
        storage.Setup(s => s.AcquireLocalPath(lutPath)).Returns(new LocalPathLease(lutPath));

        TonemapPlan plan = _selector.Build(options, null, _decisions, storage.Object);

        plan.LutFilterChain.Should().Be($"lut3d={lutPath}");
        plan.FilterStringFragment.Should().Be($"lut3d={lutPath}");
        plan.FilterStringFragment.Should().NotContain("tonemap=");

        IReadOnlyList<DecisionLog> snapshot = _decisions.Snapshot();
        snapshot.Should().Contain(d => d.Key == "plan.tonemap_resolved");
        snapshot.Should().NotContain(d => d.Key == "plan.tonemap_lut_path_rejected");
    }

    [Fact]
    public void Build_LutPath_rejected_by_storage_falls_back_to_algorithm()
    {
        string lutPath = "C:/luts/film.cube";
        HdrOptions options = new("mobius", 100, lutPath);

        Mock<IStorage> storage = new();
        storage
            .Setup(s => s.AcquireLocalPath(lutPath))
            .Throws(
                new StoragePathNotAllowedException(lutPath, "path is not under any allowed root")
            );

        TonemapPlan plan = _selector.Build(options, null, _decisions, storage.Object);

        plan.LutFilterChain.Should().BeNull();
        plan.FilterStringFragment.Should().Contain("tonemap=mobius");

        IReadOnlyList<DecisionLog> snapshot = _decisions.Snapshot();
        snapshot.Should().Contain(d => d.Key == "plan.tonemap_lut_path_rejected");
        snapshot.Should().Contain(d => d.Key == "plan.tonemap_resolved");
    }

    [Fact]
    public void Build_LutPath_set_but_no_storage_falls_through_to_algorithm()
    {
        // When IStorage is null the LUT branch is skipped — safe default so
        // callers that don't have IStorage available still get a usable plan.
        HdrOptions options = new("hable", 100, "C:/luts/film.cube");

        TonemapPlan plan = _selector.Build(options, null, _decisions, storage: null);

        plan.LutFilterChain.Should().BeNull();
        plan.FilterStringFragment.Should().Contain("tonemap=hable");
    }
}
