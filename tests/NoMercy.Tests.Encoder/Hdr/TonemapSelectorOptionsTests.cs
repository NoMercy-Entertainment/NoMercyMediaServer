// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Moq;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.Hdr;

using HdrOptions = NoMercy.Encoder.Profiles.HdrOptions;

/// <summary>
/// Tests for TonemapSelector.BuildAsync() — per-profile algorithm + LUT resolution.
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
    public async Task Build_defaults_to_hable_at_100_nits_when_no_options()
    {
        TonemapPlan plan = await _selector.BuildAsync(options: null, profileTonemapAlgorithm: null, decisions: _decisions);

        plan.Algorithm.Should().Be(expected: "hable");
        plan.PeakNits.Should().Be(expected: 100);
        plan.LutFilterChain.Should().BeNull();
        plan.FilterStringFragment.Should().Contain(expected: "tonemap=hable");
        plan.FilterStringFragment.Should().Contain(expected: "npl=100");
    }

    [Fact]
    public async Task Build_uses_HdrOptions_TonemapAlgorithm_when_set()
    {
        HdrOptions options = new(Algorithm: "mobius", PeakNits: 100);

        TonemapPlan plan = await _selector.BuildAsync(options: options, profileTonemapAlgorithm: null, decisions: _decisions);

        plan.Algorithm.Should().Be(expected: "mobius");
        plan.FilterStringFragment.Should().Contain(expected: "tonemap=mobius");
    }

    [Fact]
    public async Task Build_uses_profile_TonemapAlgorithm_when_HdrOptions_null()
    {
        TonemapPlan plan = await _selector.BuildAsync(options: null, profileTonemapAlgorithm: "reinhard", decisions: _decisions);

        plan.Algorithm.Should().Be(expected: "reinhard");
        plan.FilterStringFragment.Should().Contain(expected: "tonemap=reinhard");
    }

    [Fact]
    public async Task Build_HdrOptions_wins_over_profile_TonemapAlgorithm()
    {
        HdrOptions options = new(Algorithm: "clip", PeakNits: 100);

        TonemapPlan plan = await _selector.BuildAsync(options: options, profileTonemapAlgorithm: "reinhard", decisions: _decisions);

        plan.Algorithm.Should().Be(expected: "clip");
        plan.FilterStringFragment.Should().Contain(expected: "tonemap=clip");
        plan.FilterStringFragment.Should().NotContain(unexpected: "reinhard");
    }

    [Fact]
    public async Task Build_unknown_algorithm_falls_back_to_hable_with_decision()
    {
        HdrOptions options = new(Algorithm: "made-up", PeakNits: 100);

        TonemapPlan plan = await _selector.BuildAsync(options: options, profileTonemapAlgorithm: null, decisions: _decisions);

        plan.Algorithm.Should().Be(expected: "hable");
        plan.FilterStringFragment.Should().Contain(expected: "tonemap=hable");

        IReadOnlyList<DecisionLog> snapshot = _decisions.Snapshot();
        snapshot.Should().Contain(predicate: d => d.Key == "plan.tonemap_unknown_algorithm_defaulted");
    }

    // -------------------------------------------------------------------------
    // Peak nits
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Build_TonemapPeakNits_passes_through_to_npl_param()
    {
        HdrOptions options = new(Algorithm: "hable", PeakNits: 1000);

        TonemapPlan plan = await _selector.BuildAsync(options: options, profileTonemapAlgorithm: null, decisions: _decisions);

        plan.PeakNits.Should().Be(expected: 1000);
        plan.FilterStringFragment.Should().Contain(expected: "npl=1000");
    }

    // -------------------------------------------------------------------------
    // LUT path — IStorage mock
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Build_LutPath_set_returns_lut3d_filter_and_skips_tonemap()
    {
        string lutPath = "C:/luts/film.cube";
        HdrOptions options = new(Algorithm: "hable", PeakNits: 100, LutPath: lutPath);

        Mock<IStorage> storage = new();
        storage
            .Setup(expression: s => s.AcquireLocalPathAsync(lutPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new LocalPathLease(path: lutPath));

        TonemapPlan plan = await _selector.BuildAsync(options: options, profileTonemapAlgorithm: null, decisions: _decisions, storage: storage.Object);

        // The colon in the LUT path is filtergraph-escaped and quoted in the emitted filter.
        plan.LutFilterChain.Should().Be(expected: "lut3d='C\\:/luts/film.cube'");
        plan.FilterStringFragment.Should().Be(expected: "lut3d='C\\:/luts/film.cube'");
        plan.FilterStringFragment.Should().NotContain(unexpected: "tonemap=");

        IReadOnlyList<DecisionLog> snapshot = _decisions.Snapshot();
        snapshot.Should().Contain(predicate: d => d.Key == "plan.tonemap_resolved");
        snapshot.Should().NotContain(predicate: d => d.Key == "plan.tonemap_lut_path_rejected");
    }

    [Fact]
    public async Task Build_LutPath_rejected_by_storage_falls_back_to_algorithm()
    {
        string lutPath = "C:/luts/film.cube";
        HdrOptions options = new(Algorithm: "mobius", PeakNits: 100, LutPath: lutPath);

        Mock<IStorage> storage = new();
        storage
            .Setup(expression: s => s.AcquireLocalPathAsync(lutPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                exception: new StoragePathNotAllowedException(attemptedPath: lutPath, reason: "path is not under any allowed root")
            );

        TonemapPlan plan = await _selector.BuildAsync(options: options, profileTonemapAlgorithm: null, decisions: _decisions, storage: storage.Object);

        plan.LutFilterChain.Should().BeNull();
        plan.FilterStringFragment.Should().Contain(expected: "tonemap=mobius");

        IReadOnlyList<DecisionLog> snapshot = _decisions.Snapshot();
        snapshot.Should().Contain(predicate: d => d.Key == "plan.tonemap_lut_path_rejected");
        snapshot.Should().Contain(predicate: d => d.Key == "plan.tonemap_resolved");
    }

    [Fact]
    public async Task Build_LutPath_set_but_no_storage_falls_through_to_algorithm()
    {
        // When IStorage is null the LUT branch is skipped — safe default so
        // callers that don't have IStorage available still get a usable plan.
        HdrOptions options = new(Algorithm: "hable", PeakNits: 100, LutPath: "C:/luts/film.cube");

        TonemapPlan plan = await _selector.BuildAsync(options: options, profileTonemapAlgorithm: null, decisions: _decisions, storage: null);

        plan.LutFilterChain.Should().BeNull();
        plan.FilterStringFragment.Should().Contain(expected: "tonemap=hable");
    }
}
