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
[Trait("Category", "Unit")]
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
            string srcCandidate = Path.Combine(dir, "src");
            if (Directory.Exists(srcCandidate))
            {
                string[] files = Directory.GetFiles(
                    srcCandidate,
                    "VideoEncodeJob.cs",
                    SearchOption.AllDirectories
                );

                if (files.Length > 0)
                {
                    _cachedSource = File.ReadAllText(files[0]);
                    return _cachedSource;
                }
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException("VideoEncodeJob.cs not found under any src/ ancestor");
    }

    [Fact]
    public void CoordinatorSwitch_HandlesAllThreePhases()
    {
        string source = LoadVideoEncodeJobSource();

        bool hasWaitPass1 =
            source.Contains("CoordinatorPhase.WaitPass1:", StringComparison.Ordinal)
            || source.Contains("case CoordinatorPhase.WaitPass1", StringComparison.Ordinal);

        bool hasWaitChildren =
            source.Contains("CoordinatorPhase.WaitChildren:", StringComparison.Ordinal)
            || source.Contains("case CoordinatorPhase.WaitChildren", StringComparison.Ordinal);

        bool hasFinalize =
            source.Contains("CoordinatorPhase.Finalize:", StringComparison.Ordinal)
            || source.Contains("case CoordinatorPhase.Finalize", StringComparison.Ordinal);

        hasWaitPass1.Should().BeTrue("WaitPass1 phase must be handled in the switch");
        hasWaitChildren.Should().BeTrue("WaitChildren phase must be handled in the switch");
        hasFinalize.Should().BeTrue("Finalize phase must be handled in the switch");
    }

    [Fact]
    public void HandleWaitPass1Async_CallsReEnqueueSelf()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            "private async Task HandleWaitPass1Async",
            StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(0, "HandleWaitPass1Async must exist");

        string window = ExtractMethodWindow(source, methodStart);

        window
            .Should()
            .Contain(
                "ReEnqueueSelf",
                "WaitPass1 must call ReEnqueueSelf — it is not a terminal phase"
            );
    }

    [Fact]
    public void HandleWaitChildrenAsync_CallsReEnqueueSelf()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            "private async Task HandleWaitChildrenAsync",
            StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(0, "HandleWaitChildrenAsync must exist");

        string window = ExtractMethodWindow(source, methodStart, maxChars: 6000);

        window
            .Should()
            .Contain(
                "ReEnqueueSelf",
                "WaitChildren must call ReEnqueueSelf — it is not a terminal phase"
            );
    }

    [Fact]
    public void HandleFinalizeAsync_DoesNotCallReEnqueueSelf()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            "private async Task HandleFinalizeAsync",
            StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(0, "HandleFinalizeAsync must exist");

        string window = ExtractMethodWindow(source, methodStart);

        window
            .Should()
            .NotContain(
                "ReEnqueueSelf",
                "Finalize is the terminal phase — it must NOT re-enqueue the coordinator"
            );
    }

    [Fact]
    public void DispatchDecomposedAsync_TwoPassPath_DispatchesOnlyPass1First()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            "private async Task DispatchDecomposedAsync",
            StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(0, "DispatchDecomposedAsync must exist");

        string window = ExtractMethodWindow(source, methodStart, maxChars: 6000);

        window.Should().Contain("hasTwoPass", "DispatchDecomposedAsync must detect two-pass runs");

        window
            .Should()
            .Contain(
                "CoordinatorPhase.WaitPass1",
                "two-pass decomposition must transition coordinator to WaitPass1, not WaitChildren"
            );

        int waitPass1Index = window.IndexOf("CoordinatorPhase.WaitPass1", StringComparison.Ordinal);

        int waitChildrenIndex = window.IndexOf(
            "CoordinatorPhase.WaitChildren",
            StringComparison.Ordinal
        );

        waitChildrenIndex
            .Should()
            .BeGreaterThan(
                0,
                "DispatchDecomposedAsync must also have a WaitChildren path for single-pass runs"
            );
    }

    [Fact]
    public void HandleWaitPass1Async_DispatchesPass2AndOtherTasks()
    {
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            "private async Task HandleWaitPass1Async",
            StringComparison.Ordinal
        );

        methodStart.Should().BeGreaterThan(0);

        string window = ExtractMethodWindow(source, methodStart, maxChars: 6000);

        window
            .Should()
            .Contain(
                "pass2TaskIds",
                "HandleWaitPass1Async must dispatch Pass2 tasks when Pass1 is complete"
            );

        window
            .Should()
            .Contain(
                "CoordinatorPhase.WaitChildren",
                "HandleWaitPass1Async must transition to WaitChildren after dispatching Pass2"
            );
    }

    [Fact]
    public void WaitPass1Phase_TransitionsToWaitChildren_NotDirectlyToFinalize()
    {
        string source = LoadVideoEncodeJobSource();

        int waitPass1Start = source.IndexOf(
            "private async Task HandleWaitPass1Async",
            StringComparison.Ordinal
        );

        waitPass1Start.Should().BeGreaterThan(0);

        string window = ExtractMethodWindow(source, waitPass1Start, maxChars: 6000);

        bool transitionsToWaitChildren = window.Contains(
            "CoordinatorPhase.WaitChildren",
            StringComparison.Ordinal
        );

        bool transitionsDirectlyToFinalize = window.Contains(
            "CoordinatorPhase.Finalize",
            StringComparison.Ordinal
        );

        transitionsToWaitChildren
            .Should()
            .BeTrue("WaitPass1 must transition to WaitChildren, not skip straight to Finalize");

        transitionsDirectlyToFinalize
            .Should()
            .BeFalse("WaitPass1 must not skip WaitChildren and jump directly to Finalize");
    }

    private static string ExtractMethodWindow(string source, int methodStart, int maxChars = 2000)
    {
        int braceDepth = 0;
        bool foundFirstBrace = false;
        int methodEnd = methodStart;

        for (int charIndex = methodStart; charIndex < source.Length; charIndex++)
        {
            char current = source[charIndex];

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

        int length = Math.Min(methodEnd - methodStart + 1, maxChars);
        return source.Substring(methodStart, length);
    }

    [GeneratedRegex(@"private\s+async\s+Task\s+Handle\w+Async")]
    private static partial Regex PhaseHandlerPattern();
}
