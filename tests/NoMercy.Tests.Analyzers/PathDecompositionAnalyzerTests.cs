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
/// NMS002 — System.IO.Path decomposition methods (GetDirectoryName, GetFileName,
/// GetFileNameWithoutExtension, GetFullPath) in storage-referencing files should
/// be flagged; files without any storage reference must be left clean.
/// </summary>
public sealed class PathDecompositionAnalyzerTests
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

    private static CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> CreateTest()
    {
        return new CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
    }

    // -------------------------------------------------------------------------
    // NMS002 FIRES — every flagged decomposition method, in a storage-referencing
    // file, with the {0} message argument matching the exact method name.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(data: "GetDirectoryName")]
    [InlineData(data: "GetFileName")]
    [InlineData(data: "GetFileNameWithoutExtension")]
    [InlineData(data: "GetFullPath")]
    public async Task NMS002_Fires_ForEachFlaggedMethod_WhenFileHasStorageUsing(string methodName)
    {
        string source = $$"""
            using System.IO;
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public string Decompose(string path)
                {
                    return {|#0:Path.{{methodName}}(path)|};
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(id: PathDecompositionAnalyzer.DiagnosticId, severity: DiagnosticSeverity.Warning)
                .WithLocation(markupKey: 0)
                .WithArguments(arguments: methodName)
        );

        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 FIRES — file references IStorage as a fully-qualified parameter type.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_Fires_WhenFileUsesIStorageParameterType()
    {
        string source = """
            using System.IO;

            namespace Consumer;

            public class SomeProcessor
            {
                public string Process(NoMercy.Storage.IStorage storage, string path)
                {
                    return {|#0:Path.GetFileName(path)|};
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(id: PathDecompositionAnalyzer.DiagnosticId, severity: DiagnosticSeverity.Warning)
                .WithLocation(markupKey: 0)
                .WithArguments(arguments: "GetFileName")
        );

        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire — a Path method that is not in the flagged set
    // (e.g. HasExtension) must be left alone even in a storage-referencing file.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_ForUnflaggedPathMethod()
    {
        string source = """
            using System.IO;
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public bool HasExtension(string path)
                {
                    return Path.HasExtension(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire — file has no storage reference.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenFileHasNoStorageReference()
    {
        string source = """
            using System.IO;

            namespace Utilities;

            public class TempHelper
            {
                public string GetTempFileName(string path)
                {
                    return Path.GetFileName(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestCode = source;

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire — driver code inside NoMercy.Storage.Drivers namespace.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_InDriverNamespace()
    {
        string driver = """
            using System;
            using System.Collections.Generic;
            using System.IO;
            using NoMercy.Storage;

            namespace NoMercy.Storage.Drivers.Local;

            public class LocalStorageDriver : IStorageDriver
            {
                public string CombinePath(string parent, string child) => Path.Combine(parent, child);
                public string GetDriverRelativePath(string absolutePath) => Path.GetFileName(absolutePath);

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
                public char DirectorySeparator => Path.DirectorySeparatorChar;
                public Uri? TryGetPresignedUrl(string path, TimeSpan ttl) => null;
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("LocalStorageDriver.cs", driver));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire — pragma suppress.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenPragmaSuppressed()
    {
        string source = """
            using System.IO;
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public string GetName(string path)
                {
            #pragma warning disable NMS002
                    return Path.GetFileName(path);
            #pragma warning restore NMS002
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire — invocation target is neither member access nor a
    // bare simple name (a delegate pulled out of an array via an indexer).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_OnIndexerInvokedDelegate()
    {
        string source = """
            using System;
            using NoMercy.Storage;

            namespace Consumer;

            public class DelegateHolder
            {
                private readonly Func<string, string>[] _extractors =
                {
                    p => p,
                };

                public string GetName(string path)
                {
                    return _extractors[0](path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 FIRES — a bare "GetFileName(...)" call reached via a static import of
    // System.IO.Path (using static System.IO.Path;) must be caught, not just the
    // fully-qualified "Path.GetFileName(...)" member-access form.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_Fires_OnBareCall_ViaUsingStaticPathImport()
    {
        string source = """
            using static System.IO.Path;
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public string GetName(string path)
                {
                    return {|#0:GetFileName(path)|};
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(id: PathDecompositionAnalyzer.DiagnosticId, severity: DiagnosticSeverity.Warning)
                .WithLocation(markupKey: 0)
                .WithArguments(arguments: "GetFileName")
        );

        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire — a bare call sharing a flagged method's name, but
    // resolving to an unrelated type's method, not System.IO.Path's.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenContainingTypeIsNotSystemIOPath()
    {
        string source = """
            using NoMercy.Storage;

            namespace Consumer;

            public static class MyPathHelper
            {
                public static string GetFileName(string path) => path;
            }

            public class SomeService
            {
                public string GetName(string path)
                {
                    return MyPathHelper.GetFileName(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire, and does not crash — a call on a member that cannot be
    // resolved to any symbol at all (e.g. an undefined type, as the file might
    // look mid-edit).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenTargetSymbolIsWhollyUnresolvable()
    {
        string source = """
            using NoMercy.Storage;

            namespace Consumer;

            public class SomeService
            {
                public string GetName(string path)
                {
                    return UndefinedHelper.GetFileName(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        // Deliberately erroneous: "UndefinedHelper" does not exist. This exercises the
        // analyzer's symbol-resolution fallback (Symbol null, CandidateSymbols empty).
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire, and does not crash — call target resolves to a method
    // that is inaccessible from the call site (private, called from another type).
    // GetSymbolInfo falls back to CandidateSymbols[0] (CandidateReason.Inaccessible),
    // exercising the CandidateSymbols-non-empty branch of symbol resolution.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenTargetSymbolResolvesToInaccessibleMethodCandidate()
    {
        string source = """
            using NoMercy.Storage;

            namespace Consumer;

            public class Other
            {
                private static string GetFileName(string path) => path;
            }

            public class SomeService
            {
                public string GetName(string path)
                {
                    return Other.GetFileName(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        // Deliberately erroneous: "Other.GetFileName" is private and inaccessible from
        // "SomeService" (CS0122).
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 FIRES even when the call is outside any type declaration (top-level
    // statements) — GetContainingType must return null gracefully.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_Fires_WhenInvocationIsInTopLevelStatements()
    {
        string source = """
            using System.IO;
            using NoMercy.Storage;

            string path = "some/path.txt";
            string name = {|#0:Path.GetFileName(path)|};
            System.Console.WriteLine(name);
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(id: PathDecompositionAnalyzer.DiagnosticId, severity: DiagnosticSeverity.Warning)
                .WithLocation(markupKey: 0)
                .WithArguments(arguments: "GetFileName")
        );

        test.TestState.Sources.Add(file: ("Program.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 FIRES — a namespace that merely starts with the same characters as
    // "NoMercy.Storage.Drivers" but is not actually a descendant of it must NOT
    // be exempted.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_Fires_WhenNamespaceIsDriversLookalikeSibling_NotActualDescendant()
    {
        string source = """
            using System.IO;
            using NoMercy.Storage;

            namespace NoMercy.Storage.DriversLegacy;

            public class LegacyPathReader
            {
                public string GetName(string path)
                {
                    return {|#0:Path.GetFileName(path)|};
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.ExpectedDiagnostics.Add(
            item: new DiagnosticResult(id: PathDecompositionAnalyzer.DiagnosticId, severity: DiagnosticSeverity.Warning)
                .WithLocation(markupKey: 0)
                .WithArguments(arguments: "GetFileName")
        );

        test.TestState.Sources.Add(file: ("LegacyPathReader.cs", source));
        test.TestState.Sources.Add(file: ("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire — a "using" for a sibling namespace that merely starts
    // with the same characters as "NoMercy.Storage" (no dot boundary) must NOT be
    // treated as a real storage reference.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenUsingIsForLookalikeSiblingNamespace()
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
                public string GetName(string path)
                {
                    return Path.GetFileName(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("Marker.cs", marker));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire — an "IStorage" identifier that resolves to a type in a
    // lookalike sibling namespace (not the real NoMercy.Storage) must NOT count as
    // a storage reference.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenIStorageIdentifierResolvesToLookalikeSiblingNamespace()
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
                public string GetName(IStorage storage, string path)
                {
                    return Path.GetFileName(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        test.TestState.Sources.Add(file: ("Consumer.cs", source));
        test.TestState.Sources.Add(file: ("Marker.cs", marker));

        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // NMS002 DOES NOT fire, and does not crash — an "IStorage" identifier that is
    // AMBIGUOUS between two unrelated sibling namespaces (neither a real
    // NoMercy.Storage descendant) must not count as a storage reference, no
    // matter which ambiguous candidate the compiler picks as CandidateSymbols[0].
    // Exercises the CandidateSymbols-non-empty branch of the identifier-based
    // resolution path, deliberately using namespaces whose "using" directives do
    // NOT themselves satisfy the using-directive check, isolating this branch.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenIStorageIdentifierIsAmbiguousBetweenUnrelatedSiblings()
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
                public string GetName(IStorage storage, string path)
                {
                    return Path.GetFileName(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
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
    // NMS002 DOES NOT fire, and does not crash — an "IStorage" identifier that
    // cannot be resolved to any symbol at all (undefined type, no storage
    // reference anywhere else in the file) must not count as a storage reference.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NMS002_DoesNotFire_WhenIStorageIdentifierIsWhollyUnresolvable()
    {
        string source = """
            using System.IO;

            namespace Consumer;

            public class SomeService
            {
                public string GetName(IStorage storage, string path)
                {
                    return Path.GetFileName(path);
                }
            }
            """;

        CSharpAnalyzerTest<PathDecompositionAnalyzer, DefaultVerifier> test = CreateTest();
        // Deliberately erroneous: "IStorage" does not exist anywhere in this compilation
        // (CS0246).
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.TestState.Sources.Add(file: ("Consumer.cs", source));

        await test.RunAsync();
    }
}
