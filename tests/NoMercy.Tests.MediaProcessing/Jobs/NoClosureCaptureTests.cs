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
using NoMercy.Tests.Common;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// B1 regression guard: verifies that <c>VideoEncodeJob</c> does not capture
/// <c>MediaContext</c>, <c>FileRepository</c>, or <c>FileManager</c> via
/// closures that outlive the method that created them.
///
/// The bug pattern: a variable was captured in a lambda / local function passed
/// to EventBusProvider.EventBus.Subscribe(), causing the context to stay alive
/// after the enclosing <c>Handle()</c> invocation disposed it.
///
/// Rules enforced:
///   1. <c>EventBusProvider.EventBus.Subscribe</c> must not appear in the file.
///   2. <c>VideoEncodeJob</c> must not declare class-level fields of the
///      disqualified types (MediaContext, FileRepository, FileManager).
/// </summary>
[Trait("Category", "Unit")]
public partial class NoClosureCaptureTests
{
    private static readonly string[] DisqualifiedTypes =
    [
        "MediaContext",
        "FileRepository",
        "FileManager",
    ];

    [Fact]
    public void VideoEncodeJob_DoesNotSubscribeToEventBusInHandleMethod()
    {
        string source = ReadVideoEncodeJobSource();

        bool hasSubscribe = source.Contains(".Subscribe(", StringComparison.OrdinalIgnoreCase);

        hasSubscribe
            .Should()
            .BeFalse(
                "VideoEncodeJob must not register EventBus subscriptions — "
                    + "subscriptions would capture MediaContext across Handle() invocations (B1 regression)"
            );
    }

    [Fact]
    public void VideoEncodeJob_HasNoClassLevelDisposableFields()
    {
        string source = ReadVideoEncodeJobSource();

        List<string> violations = [];

        foreach (string typeName in DisqualifiedTypes)
        {
            if (FieldDeclarationPattern(typeName).IsMatch(source))
                violations.Add(typeName);
        }

        violations
            .Should()
            .BeEmpty(
                "MediaContext, FileRepository, and FileManager must be opened locally "
                    + "inside each phase method and disposed before the method returns. "
                    + "Class-level fields of these types would survive across Handle() calls."
            );
    }

    [Fact]
    public void VideoEncodeJob_EveryPhaseOpensOwnMediaContext()
    {
        string source = ReadVideoEncodeJobSource();

        string[] phaseMethodNames =
        [
            "HandleInitialRunAsync",
            "HandleWaitPass1Async",
            "HandleWaitChildrenAsync",
            "HandleFinalizeAsync",
        ];

        List<string> phasesWithoutContext = [];

        foreach (string methodName in phaseMethodNames)
        {
            int methodStart = source.IndexOf(
                $"private async Task {methodName}",
                StringComparison.Ordinal
            );

            if (methodStart < 0)
            {
                phasesWithoutContext.Add($"{methodName} (not found)");
                continue;
            }

            // Extract a window large enough to contain the method body opening
            int windowEnd = Math.Min(methodStart + 600, source.Length);
            string window = source[methodStart..windowEnd];

            bool hasLocalContext =
                window.Contains("await using MediaContext context", StringComparison.Ordinal)
                || window.Contains("using MediaContext context", StringComparison.Ordinal);

            if (!hasLocalContext)
                phasesWithoutContext.Add(methodName);
        }

        phasesWithoutContext
            .Should()
            .BeEmpty(
                "every coordinator phase method must open its own MediaContext locally "
                    + "so it is disposed before Handle() returns"
            );
    }

    private static string ReadVideoEncodeJobSource()
    {
        return File.ReadAllText(RepoPaths.SourceFile("VideoEncodeJob.cs"));
    }

    private static Regex FieldDeclarationPattern(string typeName) =>
        new($@"private\s+(readonly\s+)?{Regex.Escape(typeName)}\s+\w+", RegexOptions.Compiled);
}
