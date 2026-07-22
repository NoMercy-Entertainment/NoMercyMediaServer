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
/// B1 regression guard: verifies that <c>VideoEncodeJob</c> and
/// <c>EncodeTaskJob</c> do not capture <c>MediaContext</c>, <c>FileRepository</c>,
/// or <c>FileManager</c> via closures that outlive the method that created them.
///
/// The bug pattern: a variable was captured in a lambda / local function passed
/// to EventBusProvider.EventBus.Subscribe(), causing the context to stay alive
/// after the enclosing <c>Handle()</c> invocation disposed it.
///
/// Rules enforced:
///   1. <c>EventBusProvider.EventBus.Subscribe</c> must not appear in either file.
///   2. <c>VideoEncodeJob</c> must not declare class-level fields of the
///      disqualified types (MediaContext, FileRepository, FileManager).
/// </summary>
[Trait(name: "Category", value: "Unit")]
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

        bool hasSubscribe = source.Contains(value: ".Subscribe(", comparisonType: StringComparison.OrdinalIgnoreCase);

        hasSubscribe
            .Should()
            .BeFalse(
                because: "VideoEncodeJob must not register EventBus subscriptions — "
                         + "subscriptions would capture MediaContext across Handle() invocations (B1 regression)"
            );
    }

    [Fact]
    public void EncodeTaskJob_DoesNotSubscribeToEventBus()
    {
        string source = ReadEncodeTaskJobSource();

        bool hasSubscribe = source.Contains(value: ".Subscribe(", comparisonType: StringComparison.OrdinalIgnoreCase);

        hasSubscribe.Should().BeFalse(because: "EncodeTaskJob must not register EventBus subscriptions");
    }

    [Fact]
    public void VideoEncodeJob_HasNoClassLevelDisposableFields()
    {
        string source = ReadVideoEncodeJobSource();

        List<string> violations = [];

        foreach (string typeName in DisqualifiedTypes)
        {
            if (FieldDeclarationPattern(typeName: typeName).IsMatch(input: source))
                violations.Add(item: typeName);
        }

        violations
            .Should()
            .BeEmpty(
                because: "MediaContext, FileRepository, and FileManager must be opened locally "
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
                value: $"private async Task {methodName}",
                comparisonType: StringComparison.Ordinal
            );

            if (methodStart < 0)
            {
                phasesWithoutContext.Add(item: $"{methodName} (not found)");
                continue;
            }

            // Extract a window large enough to contain the method body opening
            int windowEnd = Math.Min(val1: methodStart + 600, val2: source.Length);
            string window = source[methodStart..windowEnd];

            bool hasLocalContext =
                window.Contains(value: "await using MediaContext context", comparisonType: StringComparison.Ordinal)
                || window.Contains(value: "using MediaContext context", comparisonType: StringComparison.Ordinal);

            if (!hasLocalContext)
                phasesWithoutContext.Add(item: methodName);
        }

        phasesWithoutContext
            .Should()
            .BeEmpty(
                because: "every coordinator phase method must open its own MediaContext locally "
                         + "so it is disposed before Handle() returns"
            );
    }

    private static string ReadVideoEncodeJobSource()
    {
        string path = FindSourceFile(fileName: "VideoEncodeJob.cs");
        return File.ReadAllText(path: path);
    }

    private static string ReadEncodeTaskJobSource()
    {
        string path = FindSourceFile(fileName: "EncodeTaskJob.cs");
        return File.ReadAllText(path: path);
    }

    private static string FindSourceFile(string fileName)
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;

        while (dir != null)
        {
            string candidate = Path.Combine(path1: dir, path2: "src");
            if (Directory.Exists(path: candidate))
            {
                string[] matches = Directory.GetFiles(
                    path: candidate,
                    searchPattern: fileName,
                    searchOption: SearchOption.AllDirectories
                );

                if (matches.Length > 0)
                    return matches[0];
            }

            dir = Directory.GetParent(path: dir)?.FullName;
        }

        throw new FileNotFoundException(
            message: $"Could not find source file '{fileName}' under any src/ directory"
        );
    }

    private static Regex FieldDeclarationPattern(string typeName) =>
        new(pattern: $@"private\s+(readonly\s+)?{Regex.Escape(str: typeName)}\s+\w+", options: RegexOptions.Compiled);
}
