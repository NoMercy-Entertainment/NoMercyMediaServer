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

using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Plugins;
using NoMercy.Plugins.Verification;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Updating a plugin that is installed, and in most of these also running.
/// <para>
/// The catalogue's Update button lands here. It used to unpack straight over the
/// installed folder, which cannot work: a plugin's own assembly is held for as
/// long as it is loaded, so the copy failed on the assembly every time — after
/// having already written the new manifest beside the old one. The owner got a
/// 500, and the server was left reporting a version it was not running.
/// </para>
/// <para>
/// These use the Echo sample rather than a stub, because what is under test is
/// the unload, replace and reload sequence. A fake assembly never loads, so it
/// could only ever exercise the rollback.
/// </para>
/// </summary>
public class PluginArchiveUpdateTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        $"nm-plugin-update-{Ulid.NewUlid():N}"
    );
    private readonly string _pluginsDir;
    private readonly PluginManager _manager;

    private const string FolderName = "NoMercy.Plugin.Samples.Echo";
    private const string AssemblyName = "NoMercy.Plugin.Samples.Echo.dll";
    private const string EchoId = "01ECH000000000000000000000";

    /// <summary>
    /// Spelled out rather than read off PluginManager: these name where an
    /// update is checked and where the copy it replaces waits, so a rename the
    /// boot scan was not told about fails here rather than passing by following
    /// the same constant the code does.
    /// </summary>
    private const string Staging = ".staging";

    private const string Rollback = ".rollback";

    public PluginArchiveUpdateTests()
    {
        _pluginsDir = Path.Combine(_tempDir, "plugins");
        Directory.CreateDirectory(_pluginsDir);

        _manager = new(
            new InMemoryEventBus(),
            new MinimalServiceProvider(),
            NullLogger<PluginManager>.Instance,
            _pluginsDir,
            TestStorageHelper.CreateStorage(_pluginsDir),
            TestStorageHelper.CreateBackend()
        );
    }

    public void Dispose()
    {
        _manager.Dispose();

        // The load context goes when the GC collects it, and until it does the
        // file stays held. Without this the directory below cannot be removed
        // and every run leaves one behind.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (Exception)
        {
            // Best-effort cleanup.
        }

        GC.SuppressFinalize(this);
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    /// <summary>Echo's own build output, which is a real, loadable plugin.</summary>
    private static string EchoBinDir()
    {
        string testBinDir = Path.GetDirectoryName(
            typeof(PluginArchiveUpdateTests).Assembly.Location
        )!;
        string repoRoot = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", "..", ".."));
        string tfm = Path.GetFileName(testBinDir);
        string configuration = Path.GetFileName(Path.GetDirectoryName(testBinDir)!);

        return Path.Combine(repoRoot, "tests", FolderName, "bin", configuration, tfm);
    }

    private static string Manifest(string version) =>
        $$"""
        {
          "id": "{{EchoId}}",
          "name": "Echo",
          "version": "{{version}}",
          "description": "Sample plugin for the test suite",
          "assembly": "{{AssemblyName}}",
          "autoEnabled": true
        }
        """;

    /// <summary>
    /// A published archive of Echo at a named version: its folder, every
    /// assembly it needs, its deps manifest and a plugin.json.
    /// </summary>
    private string Archive(string version, bool assemblyIsGarbage = false)
    {
        string binDir = EchoBinDir();
        string dll = Path.Combine(binDir, AssemblyName);

        if (!File.Exists(dll))
            throw new FileNotFoundException(
                $"Echo plugin DLL not found at '{dll}'. Build {FolderName} first."
            );

        string path = Path.Combine(
            _tempDir,
            $"echo-{version}{(assemblyIsGarbage ? "-broken" : "")}.zip"
        );

        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (string file in Directory.EnumerateFiles(binDir, "*.dll"))
        {
            string name = Path.GetFileName(file);

            if (assemblyIsGarbage && name == AssemblyName)
            {
                using StreamWriter broken = new(
                    archive.CreateEntry($"{FolderName}/{name}").Open(),
                    Encoding.UTF8
                );
                broken.Write("MZ but not an assembly");
                continue;
            }

            archive.CreateEntryFromFile(file, $"{FolderName}/{name}");
        }

        foreach (string file in Directory.EnumerateFiles(binDir, "*.deps.json"))
            archive.CreateEntryFromFile(file, $"{FolderName}/{Path.GetFileName(file)}");

        using (
            StreamWriter writer = new(
                archive.CreateEntry($"{FolderName}/plugin.json").Open(),
                Encoding.UTF8
            )
        )
        {
            writer.Write(Manifest(version));
        }

        return path;
    }

    private Task Install(string archivePath) =>
        _manager.InstallPluginArchiveAsync(archivePath, null, CancellationToken.None);

    private string Installed(params string[] parts) =>
        Path.Combine([_pluginsDir, FolderName, .. parts]);

    private string InstalledManifest() => File.ReadAllText(Installed("plugin.json"));

    [Fact]
    public async Task UpdatingALoadedPlugin_UnloadsItSwapsTheFilesAndLoadsTheNewCopy()
    {
        await Install(Archive("0.1.0"));

        _manager
            .GetInstalledPlugins()
            .Should()
            .ContainSingle(
                plugin => plugin.Name == "Echo",
                "the first install has to be running for the second to be an update at all"
            );

        await Install(Archive("0.2.0"));

        InstalledManifest().Should().Contain("0.2.0");
        _manager
            .GetInstalledPlugins()
            .Should()
            .ContainSingle(
                plugin => plugin.Name == "Echo",
                "an update ends with the plugin loaded, not gone"
            );
    }

    /// <summary>
    /// An update is checked while it is still staged, so a broken one never
    /// reaches the installed copy: the owner keeps the plugin they had, running,
    /// rather than finding out after the swap.
    /// </summary>
    [Fact]
    public async Task AnUpdateWhoseAssemblyIsNotOne_IsRefusedAndTheWorkingCopyKeepsRunning()
    {
        await Install(Archive("0.1.0"));

        long workingSize = new FileInfo(Installed(AssemblyName)).Length;

        Func<Task> act = () => Install(Archive("0.2.0", assemblyIsGarbage: true));

        // Refused while it is still staged. The check is a metadata read rather
        // than a load, so a truncated download or an error page saved under a
        // .dll name is caught before the working copy is touched at all.
        await act.Should().ThrowAsync<PluginVerificationException>();

        InstalledManifest()
            .Should()
            .Contain("0.1.0", "the version that worked is the one that has to be on disk");
        new FileInfo(Installed(AssemblyName))
            .Length.Should()
            .Be(workingSize, "the real assembly is untouched, not replaced by the broken one");
        _manager
            .GetInstalledPlugins()
            .Should()
            .ContainSingle(
                plugin => plugin.Name == "Echo",
                "the plugin that was running is still running"
            );
    }

    /// <summary>
    /// Nothing is written over the installed copy until the new one is complete
    /// and checked, so a failure before that point cannot reach it.
    /// </summary>
    [Fact]
    public async Task AnArchiveThatCannotBeUnpacked_LeavesTheInstalledCopyUntouched()
    {
        await Install(Archive("0.1.0"));

        string corrupt = Path.Combine(_tempDir, "corrupt.zip");
        await File.WriteAllTextAsync(corrupt, "this is not a zip");

        Func<Task> act = () => Install(corrupt);

        await act.Should().ThrowAsync<InvalidDataException>();

        InstalledManifest().Should().Contain("0.1.0");
        _manager.GetInstalledPlugins().Should().ContainSingle(plugin => plugin.Name == "Echo");
    }

    /// <summary>
    /// An archive whose manifest names an assembly it does not carry is refused
    /// while it is still staged, so the installed copy is never disturbed.
    /// </summary>
    [Fact]
    public async Task AnArchiveMissingItsAssembly_IsRefusedBeforeAnythingIsReplaced()
    {
        await Install(Archive("0.1.0"));

        string path = Path.Combine(_tempDir, "no-assembly.zip");

        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        using (
            StreamWriter writer = new(
                archive.CreateEntry($"{FolderName}/plugin.json").Open(),
                Encoding.UTF8
            )
        )
        {
            writer.Write(Manifest("0.2.0"));
        }

        Func<Task> act = () => Install(path);

        await act.Should().ThrowAsync<PluginVerificationException>();

        InstalledManifest().Should().Contain("0.1.0");
    }

    [Fact]
    public async Task ASuccessfulUpdate_LeavesNothingStagedBehind()
    {
        await Install(Archive("0.1.0"));
        await Install(Archive("0.2.0"));

        Directory
            .Exists(Path.Combine(_pluginsDir, Staging))
            .Should()
            .BeFalse("staging is where an update is checked, not where it accumulates");
    }

    /// <summary>
    /// A backup left over from an update that finished must never be put back.
    /// <para>
    /// The backup often cannot be deleted the moment the update succeeds:
    /// Windows lets a just-unloaded assembly be renamed but not yet removed, and
    /// the context goes when the GC collects it. So one can still be sitting
    /// there at the next start, and the recovery pass has to tell it apart from
    /// a backup whose update never finished - by whether the plugin folder is
    /// there. Getting that wrong would silently downgrade a plugin on every boot.
    /// </para>
    /// <para>
    /// Whether the folder is actually gone afterwards is not asserted. In a real
    /// restart nothing holds the old assembly and it is; inside one test process
    /// the previous load context may still be alive, and that is the runtime's
    /// timing rather than this behaviour.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ABackupFromAFinishedUpdate_IsNeverRestoredOverIt()
    {
        await Install(Archive("0.1.0"));
        await Install(Archive("0.2.0"));

        await _manager.LoadPluginsFromDirectoryAsync(CancellationToken.None);

        InstalledManifest()
            .Should()
            .Contain("0.2.0", "the update finished, so its backup is stale and must be ignored");
        _manager
            .GetInstalledPlugins()
            .Should()
            .ContainSingle(plugin => plugin.Name == "Echo");
    }

    /// <summary>
    /// A backup whose update never finished is the only copy of that plugin, so
    /// the same pass puts it back rather than removing it.
    /// </summary>
    [Fact]
    public async Task ABackupWhoseUpdateNeverFinished_IsRestoredOnTheNextStart()
    {
        await Install(Archive("0.1.0"));

        // What a process that died between "moved the installed copy aside" and
        // "put the new one in place" leaves behind.
        string backup = Path.Combine(_pluginsDir, Rollback, FolderName);
        Directory.CreateDirectory(Path.Combine(_pluginsDir, Rollback));
        Directory.Move(Path.Combine(_pluginsDir, FolderName), backup);

        Directory.Exists(Path.Combine(_pluginsDir, FolderName)).Should().BeFalse();

        await _manager.LoadPluginsFromDirectoryAsync(CancellationToken.None);

        File.ReadAllText(Installed("plugin.json"))
            .Should()
            .Contain("0.1.0", "the copy in the backup is the only one that plugin has left");
    }

    /// <summary>
    /// The working folders sit inside the plugins directory, so the boot scan
    /// has to know they are not plugins — loading one would run a version the
    /// rest of the server does not know about.
    /// </summary>
    [Fact]
    public async Task TheBootScan_IgnoresTheWorkingFolders()
    {
        await Install(Archive("0.1.0"));

        Directory.CreateDirectory(Path.Combine(_pluginsDir, Staging, FolderName));
        await File.WriteAllTextAsync(
            Path.Combine(_pluginsDir, Staging, FolderName, "plugin.json"),
            Manifest("9.9.9")
        );

        await _manager.LoadPluginsFromDirectoryAsync(CancellationToken.None);

        _manager
            .GetInstalledPlugins()
            .Should()
            .ContainSingle(
                plugin => plugin.Name == "Echo",
                "the staged copy is not a second installed plugin"
            );
    }
}
