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
/// NMS001 — Path.Combine in storage-referencing files should be flagged;
/// files without any storage reference must be left clean.
/// </summary>
public sealed class PathCombineAnalyzerTests
{
    // Minimal NoMercy.Storage stub — compiled as part of every test's source set
    // so the semantic model can resolve IStorage / IStorageDriver to the right namespace.
    private const string StorageStub = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;

        namespace NoMercy.Storage
        {
            public interface IStorageDriver
            {
                bool FileExists(string path);
                bool DirectoryExists(string path);
                void CreateDirectory(string path);
                void DeleteFile(string path);
                void DeleteDirectory(string path, bool recursive);
                long GetFileSize(string path);
                DateTime GetLastWriteTimeUtc(string path);
                DateTime GetCreationTimeUtc(string path);
                DateTime GetLastAccessTimeUtc(string path);
                Stream OpenRead(string path);
                Stream OpenWrite(string path, bool overwrite);
                void MoveFile(string source, string destination);
                void CopyFile(string source, string destination, bool overwrite);
                IEnumerable<string> EnumerateEntries(string path, string? pattern, bool recursive);
                string GetDriverRelativePath(string absolutePath);
                char DirectorySeparator { get; }
                Uri? TryGetPresignedUrl(string path, TimeSpan ttl);
                string CombinePath(string parent, string child);
            }

            public interface IStorage
            {
                IStorageDriver Driver { get; }
                char DirectorySeparator => Driver.DirectorySeparator;
                string CombinePath(string parent, string child) => Driver.CombinePath(parent, child);
                bool Exists(string path);
            }

            public interface IStorageFactory
            {
                IStorage Create(StorageOptions options);
            }

            public class StorageOptions { }
        }
        """;

    private static CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> CreateTest()
    {
        return new CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
    }

    // -------------------------------------------------------------------------
    // NMS001 FIRES — file has "using NoMercy.Storage"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_Fires_WhenFileHasStorageUsing()
    {
        string source = """
            using System.IO;
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public string BuildPath(string a, string b)
                {
                    return {|#0:Path.Combine(a, b)|};
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(
                id: PathCombineAnalyzer.DiagnosticId,
                severity: DiagnosticSeverity.Warning
            ).WithLocation(markupKey: 0)
        );

        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 FIRES — file references IStorage as a fully-qualified parameter type
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_Fires_WhenFileUsesIStorageParameterType()
    {
        string source = """
            using System.IO;

            namespace Consumer;

            public class SomeProcessor
            {
                public string Process(NoMercy.Storage.IStorage storage, string a, string b)
                {
                    return {|#0:Path.Combine(a, b)|};
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(
                id: PathCombineAnalyzer.DiagnosticId,
                severity: DiagnosticSeverity.Warning
            ).WithLocation(markupKey: 0)
        );

        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire — file has no storage reference (log helper, temp paths)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenFileHasNoStorageReference()
    {
        string source = """
            using System.IO;

            namespace Utilities;

            public class TempHelper
            {
                public string GetTempPath()
                {
                    return Path.Combine(Path.GetTempPath(), "nomercy_tmp");
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestCode = source;

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire — driver code inside NoMercy.Storage.Drivers namespace
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_InDriverNamespace()
    {
        string driver = """
            using System;
            using System.Collections.Generic;
            using System.IO;
            using NoMercy.Storage;

            namespace NoMercy.Storage.Drivers.Local;

            public class LocalStorageDriver : IStorageDriver
            {
                public string CombinePath(string parent, string child)
                {
                    return Path.Combine(parent, child);
                }

                public bool FileExists(string path) => File.Exists(path);
                public bool DirectoryExists(string path) => Directory.Exists(path);
                public void CreateDirectory(string path) => Directory.CreateDirectory(path);
                public void DeleteFile(string path) => File.Delete(path);
                public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
                public long GetFileSize(string path) => new FileInfo(path).Length;
                public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
                public DateTime GetCreationTimeUtc(string path) => File.GetCreationTimeUtc(path);
                public DateTime GetLastAccessTimeUtc(string path) => File.GetLastAccessTimeUtc(path);
                public Stream OpenRead(string path) => File.OpenRead(path);
                public Stream OpenWrite(string path, bool overwrite) => File.Open(path, overwrite ? FileMode.Create : FileMode.OpenOrCreate);
                public void MoveFile(string source, string destination) => File.Move(source, destination);
                public void CopyFile(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
                public IEnumerable<string> EnumerateEntries(string path, string? pattern, bool recursive) => Array.Empty<string>();
                public string GetDriverRelativePath(string absolutePath) => absolutePath;
                public char DirectorySeparator => Path.DirectorySeparatorChar;
                public Uri? TryGetPresignedUrl(string path, TimeSpan ttl) => null;
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("LocalStorageDriver.cs", driver));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire — pragma suppress
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenPragmaSuppressed()
    {
        string source = """
            using System.IO;
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public string BuildOsPath(string a, string b)
                {
            #pragma warning disable NMS001
                    return Path.Combine(a, b);
            #pragma warning restore NMS001
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire — invocation target is neither member access nor a
    // bare simple name (e.g. a delegate pulled out of an array via an indexer).
    // The analyzer intentionally leaves these unresolved rather than guessing.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_OnIndexerInvokedDelegate()
    {
        string source = """
            using System;
            using NoMercy.Storage;

            namespace Consumer;

            public class DelegateHolder
            {
                private readonly Func<string, string, string>[] _combiners =
                {
                    (a, b) => a + "/" + b,
                };

                public string BuildPath(string a, string b)
                {
                    return _combiners[0](a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 FIRES — a bare "Combine(...)" call reached via a static import of
    // System.IO.Path (using static System.IO.Path;) must be caught, not just the
    // fully-qualified "Path.Combine(...)" member-access form.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_Fires_OnBareCombineCall_ViaUsingStaticPathImport()
    {
        string source = """
            using static System.IO.Path;
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public string BuildPath(string a, string b)
                {
                    return {|#0:Combine(a, b)|};
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(
                id: PathCombineAnalyzer.DiagnosticId,
                severity: DiagnosticSeverity.Warning
            ).WithLocation(markupKey: 0)
        );

        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire — a bare call named "Combine" that is NOT System.IO.Path.Combine
    // (a local static import of some other type's Combine method) must not be flagged.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_OnBareCombineCall_FromUnrelatedStaticImport()
    {
        string source = """
            using static Consumer.MyPathHelper;
            using NoMercy.Storage;

            namespace Consumer;

            public static class MyPathHelper
            {
                public static string Combine(string a, string b) => a + "/" + b;
            }

            public class SomeService
            {
                public string BuildPath(string a, string b)
                {
                    return Combine(a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire — member access named "Combine" that resolves to a
    // method on some other type, not System.IO.Path.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenContainingTypeIsNotSystemIOPath()
    {
        string source = """
            using NoMercy.Storage;

            namespace Consumer;

            public static class MyPathHelper
            {
                public static string Combine(string a, string b) => a + "/" + b;
            }

            public class SomeService
            {
                public string BuildPath(string a, string b)
                {
                    return MyPathHelper.Combine(a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire, and does not crash — a "Combine(...)" call on a member
    // that cannot be resolved to any symbol at all (e.g. an undefined type, as the
    // file might look mid-edit). The analyzer must degrade gracefully on erroneous
    // code instead of throwing, since it runs on every keystroke in the IDE.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenTargetSymbolIsWhollyUnresolvable()
    {
        string source = """
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public string BuildPath(string a, string b)
                {
                    return UndefinedHelper.Combine(a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        // Deliberately erroneous: "UndefinedHelper" does not exist. This exercises the
        // analyzer's symbol-resolution fallback (Symbol null, CandidateSymbols empty),
        // not the compiler diagnostics themselves.
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire, and does not crash — "Combine(...)" invoked on a method
    // that is inaccessible from the call site (private, called from another type).
    // GetSymbolInfo cannot resolve Symbol directly here, so it must fall back to
    // CandidateSymbols[0] (CandidateReason.Inaccessible) — exercising the
    // CandidateSymbols-non-empty branch of symbol resolution.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenTargetSymbolResolvesToInaccessibleMethodCandidate()
    {
        string source = """
            using NoMercy.Storage;

            namespace Consumer;

            public class Other
            {
                private static string Combine(string a, string b) => a + b;
            }

            public class SomeService
            {
                public string BuildPath(string a, string b)
                {
                    return Other.Combine(a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        // Deliberately erroneous: "Other.Combine" is private and inaccessible from
        // "SomeService" (CS0122). This exercises the CandidateSymbols fallback landing
        // on a real IMethodSymbol whose ContainingType is not System.IO.Path.
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 FIRES even when Path.Combine is invoked outside any type declaration
    // (top-level statements) — GetContainingType must return null gracefully and
    // the driver-exemption check must simply be skipped, not crash or suppress.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_Fires_WhenInvocationIsInTopLevelStatements()
    {
        string source = """
            using System.IO;
            using NoMercy.Storage;

            string a = "one";
            string b = "two";
            string combined = {|#0:Path.Combine(a, b)|};
            System.Console.WriteLine(combined);
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(
                id: PathCombineAnalyzer.DiagnosticId,
                severity: DiagnosticSeverity.Warning
            ).WithLocation(markupKey: 0)
        );

        test.TestState.Sources.Add(file: ("Program.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 FIRES — a namespace that merely starts with the same characters as
    // "NoMercy.Storage.Drivers" but is not actually a descendant of it (a sibling,
    // e.g. "NoMercy.Storage.DriversLegacy") must NOT be exempted. A naive
    // string.StartsWith check would incorrectly treat it as driver code.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_Fires_WhenNamespaceIsDriversLookalikeSibling_NotActualDescendant()
    {
        string source = """
            using System.IO;
            using NoMercy.Storage;

            namespace NoMercy.Storage.DriversLegacy;

            public class LegacyPathBuilder
            {
                public string BuildPath(string a, string b)
                {
                    return {|#0:Path.Combine(a, b)|};
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(
                id: PathCombineAnalyzer.DiagnosticId,
                severity: DiagnosticSeverity.Warning
            ).WithLocation(markupKey: 0)
        );

        test.TestState.Sources.Add(file: ("LegacyPathBuilder.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire — a "using" for a sibling namespace that merely starts
    // with the same characters as "NoMercy.Storage" (e.g. "NoMercy.StorageEngine",
    // no dot boundary) must NOT be treated as a real storage reference.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenUsingIsForLookalikeSiblingNamespace()
    {
        string marker = """
            namespace NoMercy.StorageEngine
            {
                public class Marker { }
            }
            """;

        string source = """
            using System.IO;
            using NoMercy.StorageEngine;

            namespace Consumer;

            public class SomeService
            {
                public string BuildPath(string a, string b)
                {
                    return Path.Combine(a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("Marker.cs", marker));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire — an "IStorage" identifier that resolves to a type in a
    // lookalike sibling namespace (not the real NoMercy.Storage) must NOT count as
    // a storage reference.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenIStorageIdentifierResolvesToLookalikeSiblingNamespace()
    {
        string marker = """
            namespace NoMercy.StorageEngine
            {
                public interface IStorage { }
            }
            """;

        string source = """
            using System.IO;
            using NoMercy.StorageEngine;

            namespace Consumer;

            public class SomeService
            {
                public string BuildPath(IStorage storage, string a, string b)
                {
                    return Path.Combine(a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("Marker.cs", marker));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire, and does not crash — an "IStorage" identifier that is
    // AMBIGUOUS between two unrelated sibling namespaces (neither a real
    // NoMercy.Storage descendant) must not count as a storage reference, no
    // matter which ambiguous candidate the compiler picks as CandidateSymbols[0].
    // Exercises the CandidateSymbols-non-empty branch of the identifier-based
    // resolution path, deliberately using namespaces whose "using" directives do
    // NOT themselves satisfy the using-directive check, isolating this branch.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenIStorageIdentifierIsAmbiguousBetweenUnrelatedSiblings()
    {
        string markers = """
            namespace NoMercy.StorageEngineA
            {
                public interface IStorage { }
            }

            namespace NoMercy.StorageEngineB
            {
                public interface IStorage { }
            }
            """;

        string source = """
            using System.IO;
            using NoMercy.StorageEngineA;
            using NoMercy.StorageEngineB;

            namespace Consumer;

            public class SomeService
            {
                public string BuildPath(IStorage storage, string a, string b)
                {
                    return Path.Combine(a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        // Deliberately erroneous: "IStorage" is ambiguous between StorageEngineA and
        // StorageEngineB (CS0104). Neither is a real descendant of NoMercy.Storage, and
        // neither using directive matches the using-directive check either, so the
        // outcome is deterministic regardless of tie-break order between candidates.
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("Markers.cs", markers));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS001 DOES NOT fire, and does not crash — an "IStorage" identifier that
    // cannot be resolved to any symbol at all (undefined type, no storage
    // reference anywhere else in the file) must not count as a storage reference.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS001_DoesNotFire_WhenIStorageIdentifierIsWhollyUnresolvable()
    {
        string source = """
            using System.IO;

            namespace Consumer;

            public class SomeService
            {
                public string BuildPath(IStorage storage, string a, string b)
                {
                    return Path.Combine(a, b);
                }
            }
            """;

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = CreateTest();
        // Deliberately erroneous: "IStorage" does not exist anywhere in this compilation
        // (CS0246). This exercises the sym-is-null branch of the identifier resolution
        // fallback, distinct from the invocation-symbol fallback tested elsewhere.
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.TestState.Sources.Add(file: ("Consumer.cs", source));

        await test.RunAsync();
    }
}
