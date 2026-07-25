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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NoMercy.Analyzers;
using Xunit;

namespace NoMercy.Tests.Analyzers;

/// <summary>
/// CB001 is the rule that deals with opaque callbacks: <c>e =&gt; e.Something</c>
/// becomes <c>episode =&gt; episode.Something</c>, taking the name from the delegate's
/// own parameter type rather than a generic placeholder. These tests pin the
/// resulting text, because the value of the rule is entirely in what it renames to.
/// </summary>
public sealed class CallbackParameterCodeFixProviderTests
{
    private const string Model = """
        namespace Consumer;

        public class Episode
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
        }
        """;

    private static CSharpCodeFixTest<
        CallbackParameterAnalyzer,
        CallbackParameterCodeFixProvider,
        DefaultVerifier
    > CreateTest()
    {
        return new CSharpCodeFixTest<
            CallbackParameterAnalyzer,
            CallbackParameterCodeFixProvider,
            DefaultVerifier
        >
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
    }

    private static async Task VerifyFixAsync(string before, string after, int diagnostics)
    {
        CSharpCodeFixTest<
            CallbackParameterAnalyzer,
            CallbackParameterCodeFixProvider,
            DefaultVerifier
        > test = CreateTest();

        for (int location = 0; location < diagnostics; location++)
        {
            test.ExpectedDiagnostics.Add(
                new DiagnosticResult(
                    DiagnosticIds.CallbackParameter,
                    DiagnosticSeverity.Warning
                ).WithLocation(location)
            );
        }

        test.TestState.Sources.Add(("Consumer.cs", before));
        test.TestState.Sources.Add(("Model.cs", Model));
        test.FixedState.Sources.Add(("Consumer.cs", after));
        test.FixedState.Sources.Add(("Model.cs", Model));

        await test.RunAsync();
    }

    [Fact]
    public async Task CB001_RenamesSingleLetterParameter_ToItsDelegateParameterType()
    {
        string before = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public bool Go(List<Episode> episodes) => episodes.Any({|#0:e|} => e.Id > 0);
            }
            """;

        string after = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public bool Go(List<Episode> episodes) => episodes.Any(episode => episode.Id > 0);
            }
            """;

        await VerifyFixAsync(before, after, 1);
    }

    [Fact]
    public async Task CB001_RenamesEveryUsageInsideTheLambdaBody()
    {
        string before = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public List<string> Go(List<Episode> episodes) =>
                    episodes.Where({|#0:e|} => e.Id > 0 && e.Title.Length > 1).Select({|#1:e|} => e.Title).ToList();
            }
            """;

        string after = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public List<string> Go(List<Episode> episodes) =>
                    episodes.Where(episode => episode.Id > 0 && episode.Title.Length > 1).Select(episode => episode.Title).ToList();
            }
            """;

        await VerifyFixAsync(before, after, 2);
    }

    [Fact]
    public async Task CB001_DoesNotFire_OnAlreadyDescriptiveParameter()
    {
        string source = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public bool Go(List<Episode> episodes) => episodes.Any(episode => episode.Id > 0);
            }
            """;

        await VerifyFixAsync(source, source, 0);
    }

    [Fact]
    public async Task CB001_DoesNotFire_OnDiscardParameter()
    {
        string source = """
            using System;

            namespace Consumer;

            public class SomeService
            {
                private static void Run(Action<int> callback) { }

                public void Go() => Run(_ => { });
            }
            """;

        await VerifyFixAsync(source, source, 0);
    }
}
