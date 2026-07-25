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
/// NA0001 asks for a named argument in exactly two situations: the call has more
/// than three arguments, or the value is a bare true/false/null whose parameter
/// cannot be inferred at the call site. Renaming an opaque callback parameter is
/// CB001's job, not this rule's.
/// </summary>
/// <remarks>
/// The "does not fire" cases carry the weight here. An earlier version flagged
/// every positional argument of every method with more than two parameters, and a
/// solution-wide fix-all of that turned into thousands of unwanted edits.
/// </remarks>
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

    private static async Task VerifyDiagnosticsAsync(string source, int count)
    {
        CSharpAnalyzerTest<NamedArgumentsAnalyzer, DefaultVerifier> test = CreateTest();
        for (int location = 0; location < count; location++)
        {
            test.ExpectedDiagnostics.Add(
                new DiagnosticResult(
                    DiagnosticIds.RequireNamedArguments,
                    DiagnosticSeverity.Warning
                ).WithLocation(location)
            );
        }

        test.TestState.Sources.Add(("Consumer.cs", source));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // FIRES — a bare boolean says nothing about which parameter it lands on
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NA0001_Fires_OnBareBooleanLiteral()
    {
        string source = """
            namespace Consumer;

            public class SomeService
            {
                private static void Detect(string fingerprints, bool fromTail) { }

                public void Go()
                {
                    Detect("abc", {|#0:true|});
                }
            }
            """;

        await VerifyDiagnosticsAsync(source, 1);
    }

    [Fact]
    public async Task NA0001_Fires_OnBareNullLiteral()
    {
        string source = """
            namespace Consumer;

            public class SomeService
            {
                private static void Load(string path, string? overrideName) { }

                public void Go()
                {
                    Load("a", {|#0:null|});
                }
            }
            """;

        await VerifyDiagnosticsAsync(source, 1);
    }

    // -------------------------------------------------------------------------
    // FIRES — more than three arguments
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NA0001_Fires_OnEveryArgumentOfACallWithMoreThanThree()
    {
        string source = """
            namespace Consumer;

            public class SomeService
            {
                private static void Build(string a, string b, string c, string d) { }

                public void Go()
                {
                    Build({|#0:"1"|}, {|#1:"2"|}, {|#2:"3"|}, {|#3:"4"|});
                }
            }
            """;

        await VerifyDiagnosticsAsync(source, 4);
    }

    // -------------------------------------------------------------------------
    // DOES NOT FIRE — the noise the old rule produced
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NA0001_DoesNotFire_OnThreeArguments()
    {
        string source = """
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
    public async Task NA0001_DoesNotFire_OnSingleArgumentCall()
    {
        string source = """
            using System.Collections.Generic;

            namespace Consumer;

            public class SomeService
            {
                public string Go(List<string> values) => string.Join(",", values);
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_OnSingleLetterCallbackLambda()
    {
        // Renaming `a` is CB001's job. NA0001 must leave short LINQ calls alone,
        // otherwise every lambda in the codebase grows a `predicate:` prefix.
        string source = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                public int CountMatches(List<string> values)
                {
                    return values.Count(a => a.Length > 2);
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_WhenArgumentIsAlreadyNamed()
    {
        string source = """
            namespace Consumer;

            public class SomeService
            {
                private static void Detect(string fingerprints, bool fromTail) { }

                public void Go()
                {
                    Detect("abc", fromTail: true);
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_OnBooleanInsideParamsTail()
    {
        string source = """
            namespace Consumer;

            public class SomeService
            {
                private static void Run(params bool[] flags) { }

                public void Go()
                {
                    Run(true);
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }

    [Fact]
    public async Task NA0001_DoesNotFire_OnNonLiteralBooleanExpression()
    {
        // `values.Any()` names itself; only a naked true/false is opaque.
        string source = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Consumer;

            public class SomeService
            {
                private static void Detect(string fingerprints, bool fromTail) { }

                public void Go(List<string> values)
                {
                    Detect("abc", values.Any());
                }
            }
            """;

        await VerifyNoDiagnosticAsync(source);
    }
}
