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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles.V2;

public class DiskOverlayLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public DiskOverlayLoaderTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"nomercy-overlay-{Ulid.NewUlid()}");
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(path: _tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void Loads_wrapper_form()
    {
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "wrap.json"),
            contents: """
                      {
                          "name": "Wrapped",
                          "profile": { "id": "01HQ6298ZS00000000000000AA", "name": "Wrapped", "container": 3, "audio": [], "subtitles": [] }
                      }
                      """
        );

        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: _tempDir);
        result.Loaded.Should().HaveCount(expected: 1);
        result.Loaded[index: 0].Profile.Name.Should().Be(expected: "Wrapped");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Loads_raw_form()
    {
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "raw.json"),
            contents: """
                      { "id": "01HQ6298ZS00000000000000BB", "name": "Raw", "container": 3, "audio": [], "subtitles": [] }
                      """
        );

        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: _tempDir);
        result.Loaded.Should().HaveCount(expected: 1);
        result.Loaded[index: 0].Profile.Name.Should().Be(expected: "Raw");
    }

    [Fact]
    public void Bad_json_logs_error_skips_file_continues()
    {
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "good.json"),
            contents: """
                      { "name": "Good", "profile": { "id": "01HQ6298ZS00000000000000CC", "name": "Good", "container": 3, "audio": [], "subtitles": [] } }
                      """
        );
        File.WriteAllText(path: Path.Combine(path1: _tempDir, path2: "bad.json"), contents: "{not json");

        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: _tempDir);
        result.Loaded.Should().HaveCount(expected: 1);
        result.Loaded[index: 0].Profile.Name.Should().Be(expected: "Good");
        result.Errors.Should().HaveCount(expected: 1);
        result.Errors[index: 0].Should().Contain(expected: "bad.json");
    }

    [Fact]
    public void Forward_compat_extra_keys_tolerated()
    {
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "extra.json"),
            contents: """
                      {
                          "name": "Extra",
                          "futureField": "ignored",
                          "profile": {
                              "id": "01HQ6298ZS00000000000000DD",
                              "name": "Extra",
                              "container": 3,
                              "audio": [],
                              "subtitles": [],
                              "anotherFutureField": 42
                          }
                      }
                      """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: _tempDir);
        result.Loaded.Should().HaveCount(expected: 1);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Empty_file_logs_error_continues()
    {
        File.WriteAllText(path: Path.Combine(path1: _tempDir, path2: "empty.json"), contents: "");
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "ok.json"),
            contents: """
                      { "id": "01HQ6298ZS00000000000000EE", "name": "Ok", "container": 3, "audio": [], "subtitles": [] }
                      """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: _tempDir);
        result.Loaded.Should().HaveCount(expected: 1);
        result.Errors.Should().HaveCount(expected: 1);
    }

    [Fact]
    public void Mixed_wrapper_and_raw_both_load()
    {
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "w.json"),
            contents: """
                      { "name": "W", "profile": { "id": "01HQ6298ZS00000000000000FF", "name": "W", "container": 3, "audio": [], "subtitles": [] } }
                      """
        );
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "r.json"),
            contents: """
                      { "id": "01HQ6298ZS0000000000000100", "name": "R", "container": 3, "audio": [], "subtitles": [] }
                      """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: _tempDir);
        result.Loaded.Should().HaveCount(expected: 2);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Subdirectories_not_scanned_recursively()
    {
        string sub = Path.Combine(path1: _tempDir, path2: "sub");
        Directory.CreateDirectory(path: sub);
        File.WriteAllText(
            path: Path.Combine(path1: sub, path2: "deep.json"),
            contents: """
                      { "id": "01HQ6298ZS0000000000000111", "name": "Deep", "container": 3, "audio": [], "subtitles": [] }
                      """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: _tempDir);
        result.Loaded.Should().BeEmpty();
    }

    [Fact]
    public void Missing_directory_returns_empty()
    {
        string missing = Path.Combine(path1: _tempDir, path2: "nope");
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: missing);
        result.Loaded.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_ulid_across_files_warns_but_loads_both()
    {
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "first.json"),
            contents: """
                      { "id": "01HQ6298ZS0000000000000222", "name": "First", "container": 3, "audio": [], "subtitles": [] }
                      """
        );
        File.WriteAllText(
            path: Path.Combine(path1: _tempDir, path2: "second.json"),
            contents: """
                      { "id": "01HQ6298ZS0000000000000222", "name": "Second", "container": 3, "audio": [], "subtitles": [] }
                      """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(directory: _tempDir);
        result.Loaded.Should().HaveCount(expected: 2);
        result.Errors.Should().HaveCount(expected: 1);
        result.Errors[index: 0].Should().Contain(expected: "duplicate");
    }
}
