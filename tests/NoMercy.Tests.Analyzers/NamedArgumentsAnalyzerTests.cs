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
/// NA0001 fires on exactly one shape: a positional argument that is a callback
/// lambda whose parameter is a single letter. It previously flagged every
/// positional argument of every method with more than two parameters, which a
/// solution-wide fix-all turned into thousands of unwanted edits — so the
/// "does not fire" cases below are the point of the rule, not filler.
/// </summary>
public sealed class NamedArgumentsAnalyzerTests
{
    private static CSharpAnalyzerTest<NamedArgumentsAnalyzer, DefaultVerifier> CreateTest()
    {
        return new CSharpAnalyzerTest<NamedArgumentsAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
    }

    private static async Task VerifyNoDiagnosticAsync(string source)
    {
        CSharpAnalyzerTest<NamedArgumentsAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(("Consumer.cs", source));

        await test.RunAsync();
    }

    private static async Task VerifyDiagnosticAsync(string source)
    {
        CSharpAnalyzerTest<NamedArgumentsAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(
                DiagnosticIds.RequireNamedArguments,
                DiagnosticSeverity.Warning
            ).WithLocation(0)
        );
        test.TestState.Sources.Add(("Consumer.cs", source));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // FIRES — callback lambda with a single-letter parameter
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NA0001_Fires_OnSingleLetterSimpleLambda()
    {
        string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public int CountMatches(List<string> values)
                {
                    return values.Count({|#0:a => a.Length > 2|});
                }
            }
            """;

        await VerifyDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_Fires_OnParenthesizedLambdaWithSingleLetterParameter()
    {
        string source = """
            using System;

            namespace Consumer;

            public class SomeService
            {
                private static void Run(Func<int, int, int> combine) { }

                public void Go()
                {
                    Run({|#0:(x, total) => x + total|});
                }
            }
            """;

        await VerifyDiagnosticAsync(source);
    }

    // -------------------------------------------------------------------------
    // DOES NOT FIRE — everything the old rule wrongly flagged
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NA0001_DoesNotFire_OnPlainPositionalArguments()
    {
        string source = """
            using System;

            namespace Consumer;

            public class SomeService
            {
                private static string Build(string first, string second, string third) => first;

                public string Go()
                {
                    return Build("a", "b", "c");
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_OnDescriptiveLambdaParameter()
    {
        string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public int CountMatches(List<string> values)
                {
                    return values.Count(value => value.Length > 2);
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_OnDiscardLambdaParameter()
    {
        string source = """
            using System;

            namespace Consumer;

            public class SomeService
            {
                private static void Run(Action<int> callback) { }

                public void Go()
                {
                    Run(_ => { });
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_WhenArgumentIsAlreadyNamed()
    {
        string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public int CountMatches(List<string> values)
                {
                    return values.Count(predicate: a => a.Length > 2);
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_OnSingleLetterLambdaInParamsTail()
    {
        string source = """
            using System;

            namespace Consumer;

            public class SomeService
            {
                private static void Run(params Func<int, bool>[] predicates) { }

                public void Go()
                {
                    Run(a => a > 1);
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_OnMethodGroupArgument()
    {
        string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                private static bool IsLong(string value) => value.Length > 2;

                public int CountMatches(List<string> values)
                {
                    return values.Count(IsLong);
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }
}
