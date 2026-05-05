using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NoMercy.Storage.Analyzers;
using Xunit;

namespace NoMercy.Tests.Storage.Analyzers;

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

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(
                    PathCombineAnalyzer.DiagnosticId,
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Warning
                ).WithLocation(0),
            },
        };

        test.TestState.Sources.Add(("Consumer.cs", source));
        test.TestState.Sources.Add(("StorageStub.cs", StorageStub));

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

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(
                    PathCombineAnalyzer.DiagnosticId,
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Warning
                ).WithLocation(0),
            },
        };

        test.TestState.Sources.Add(("Consumer.cs", source));
        test.TestState.Sources.Add(("StorageStub.cs", StorageStub));

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

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

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

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.TestState.Sources.Add(("LocalStorageDriver.cs", driver));
        test.TestState.Sources.Add(("StorageStub.cs", StorageStub));

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

        CSharpAnalyzerTest<PathCombineAnalyzer, DefaultVerifier> test = new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.TestState.Sources.Add(("Consumer.cs", source));
        test.TestState.Sources.Add(("StorageStub.cs", StorageStub));

        await test.RunAsync();
    }
}
