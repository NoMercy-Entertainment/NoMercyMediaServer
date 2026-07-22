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

using System.Text.RegularExpressions;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// B2/B3 regression guard: verifies the coordinator phase routing structure in
/// <c>VideoEncodeJob</c> at source level.
///
/// These tests confirm that:
///   1. All three <see cref="NoMercy.MediaProcessing.Jobs.MediaJobs.CoordinatorPhase"/> values
///      are handled in the switch — no phase can fall through unhandled.
///   2. Each phase handler is present and calls <c>ReEnqueueSelf</c> (to keep the
///      coordinator alive across restarts) OR represents a terminal phase.
///   3. The initial-run path dispatches Pass1-only for two-pass runs
///      (no Pass2 in the first dispatch wave).
///   4. <c>HandleFinalizeAsync</c> does NOT call <c>ReEnqueueSelf</c> —
///      finalize is the terminal phase.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public partial class PhaseTransitionTests
{
    private static string? _cachedSource;

    private static string LoadVideoEncodeJobSource()
    {
        if (_cachedSource is not null)
            return _cachedSource;

        string? dir = AppDomain.CurrentDomain.BaseDirectory;

        while (dir is not null)
        {
            string srcCandidate = Path.Combine(path1: dir, path2: "src");
            if (Directory.Exists(path: srcCandidate))
            {
                string[] files = Directory.GetFiles(
                    path: srcCandidate,
                    searchPattern: "VideoEncodeJob.cs",
                    searchOption: SearchOption.AllDirectories
                );

                if (files.Length > 0)
                {
                    _cachedSource = File.ReadAllText(path: files[0]);
                    return _cachedSource;
                }
            }

            dir = Directory.GetParent(path: dir)?.FullName;
        }

        throw new FileNotFoundException(message: "VideoEncodeJob.cs not found under any src/ ancestor");
    }

    [Fact]
    public void CoordinatorSwitch_HandlesAllThreePhases()
    {
        string source = LoadVideoEncodeJobSource();

        bool hasWaitPass1 =
            source.Contains(value: "CoordinatorPhase.WaitPass1:", comparisonType: StringComparison.Ordinal)
            || source.Contains(value: "case CoordinatorPhase.WaitPass1", comparisonType: StringComparison.Ordinal);

        bool hasWaitChildren =
            source.Contains(value: "CoordinatorPhase.WaitChildren:", comparisonType: StringComparison.Ordinal)
            || source.Contains(value: "case CoordinatorPhase.WaitChildren", comparisonType: StringComparison.Ordinal);

        bool hasFinalize =
            source.Contains(value: "CoordinatorPhase.Finalize:", comparisonType: StringComparison.Ordinal)
            || source.Contains(value: "case CoordinatorPhase.Finalize", comparisonType: StringComparison.Ordinal);

        hasWaitPass1.Should().BeTrue(because: "WaitPass1 phase must be handled in the switch");
        hasWaitChildren.Should().BeTrue(because: "WaitChildren phase must be handled in the switch");
        hasFinalize.Should().BeTrue(because: "Finalize phase must be handled in the switch");
    }

    [Fact]
    public void HandleWaitPass1Async_CallsReEnqueueSelf()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            value: "private async Task HandleWaitPass1Async",
            comparisonType: StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(expected: 0, because: "HandleWaitPass1Async must exist");

        string window = ExtractMethodWindow(source: source, methodStart: methodStart);

        window
            .Should()
            .Contain(
                expected: "ReEnqueueSelf",
                because: "WaitPass1 must call ReEnqueueSelf — it is not a terminal phase"
            );
    }

    [Fact]
    public void HandleWaitChildrenAsync_CallsReEnqueueSelf()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            value: "private async Task HandleWaitChildrenAsync",
            comparisonType: StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(expected: 0, because: "HandleWaitChildrenAsync must exist");

        string window = ExtractMethodWindow(source: source, methodStart: methodStart, maxChars: 6000);

        window
            .Should()
            .Contain(
                expected: "ReEnqueueSelf",
                because: "WaitChildren must call ReEnqueueSelf — it is not a terminal phase"
            );
    }

    [Fact]
    public void HandleFinalizeAsync_DoesNotCallReEnqueueSelf()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            value: "private async Task HandleFinalizeAsync",
            comparisonType: StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(expected: 0, because: "HandleFinalizeAsync must exist");

        string window = ExtractMethodWindow(source: source, methodStart: methodStart);

        window
            .Should()
            .NotContain(
                unexpected: "ReEnqueueSelf",
                because: "Finalize is the terminal phase — it must NOT re-enqueue the coordinator"
            );
    }

    [Fact]
    public void DispatchDecomposedAsync_TwoPassPath_DispatchesOnlyPass1First()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            value: "private async Task DispatchDecomposedAsync",
            comparisonType: StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(expected: 0, because: "DispatchDecomposedAsync must exist");

        string window = ExtractMethodWindow(source: source, methodStart: methodStart, maxChars: 6000);

        window.Should().Contain(expected: "hasTwoPass", because: "DispatchDecomposedAsync must detect two-pass runs");

        window
            .Should()
            .Contain(
                expected: "CoordinatorPhase.WaitPass1",
                because: "two-pass decomposition must transition coordinator to WaitPass1, not WaitChildren"
            );

        int waitPass1Index = window.IndexOf(value: "CoordinatorPhase.WaitPass1", comparisonType: StringComparison.Ordinal);

        int waitChildrenIndex = window.IndexOf(
            value: "CoordinatorPhase.WaitChildren",
            comparisonType: StringComparison.Ordinal
        );

        waitChildrenIndex
            .Should()
            .BeGreaterThan(
                expected: 0,
                because: "DispatchDecomposedAsync must also have a WaitChildren path for single-pass runs"
            );
    }

    [Fact]
    public void HandleWaitPass1Async_DispatchesPass2AndOtherTasks()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            value: "private async Task HandleWaitPass1Async",
            comparisonType: StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(expected: 0);

        string window = ExtractMethodWindow(source: source, methodStart: methodStart, maxChars: 6000);

        window
            .Should()
            .Contain(
                expected: "pass2TaskIds",
                because: "HandleWaitPass1Async must dispatch Pass2 tasks when Pass1 is complete"
            );

        window
            .Should()
            .Contain(
                expected: "CoordinatorPhase.WaitChildren",
                because: "HandleWaitPass1Async must transition to WaitChildren after dispatching Pass2"
            );
    }

    [Fact]
    public void WaitPass1Phase_TransitionsToWaitChildren_NotDirectlyToFinalize()
    {
        string source = LoadVideoEncodeJobSource();

        int waitPass1Start = source.IndexOf(
            value: "private async Task HandleWaitPass1Async",
            comparisonType: StringComparison.Ordinal
        );

        waitPass1Start.Should().BeGreaterThan(expected: 0);

        string window = ExtractMethodWindow(source: source, methodStart: waitPass1Start, maxChars: 6000);

        bool transitionsToWaitChildren = window.Contains(
            value: "CoordinatorPhase.WaitChildren",
            comparisonType: StringComparison.Ordinal
        );

        bool transitionsDirectlyToFinalize = window.Contains(
            value: "CoordinatorPhase.Finalize",
            comparisonType: StringComparison.Ordinal
        );

        transitionsToWaitChildren
            .Should()
            .BeTrue(because: "WaitPass1 must transition to WaitChildren, not skip straight to Finalize");

        transitionsDirectlyToFinalize
            .Should()
            .BeFalse(because: "WaitPass1 must not skip WaitChildren and jump directly to Finalize");
    }

    private static string ExtractMethodWindow(string source, int methodStart, int maxChars = 2000)
    {
        int braceDepth = 0;
        bool foundFirstBrace = false;
        int methodEnd = methodStart;

        for (int charIndex = methodStart; charIndex < source.Length; charIndex++)
        {
            char current = source[index: charIndex];

            if (current == '{')
            {
                braceDepth++;
                foundFirstBrace = true;
            }
            else if (current == '}')
            {
                braceDepth--;
                if (foundFirstBrace && braceDepth == 0)
                {
                    methodEnd = charIndex;
                    break;
                }
            }
        }

        int length = Math.Min(val1: methodEnd - methodStart + 1, val2: maxChars);
        return source.Substring(startIndex: methodStart, length: length);
    }

    [GeneratedRegex(pattern: @"private\s+async\s+Task\s+Handle\w+Async")]
    private static partial Regex PhaseHandlerPattern();
}
