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

using Moq;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Output;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.Storage;

/// <summary>
/// Phase 4.15 — output path allowlist integration + stress.
///
/// Sub-tasks verified here:
///   1. Audit: encoder has no raw System.IO.File.* bypass — confirmed by
///      <see cref="IStorageAdoptionTests"/> + static grep (OutputArtifact.cs
///      only calls Path.GetExtension, which is string-only). This file adds
///      the regression assertion.
///   2. Allowlist config: library roots, output roots, live-transcode cache,
///      remote-source cache, Tesseract data dir, rip output, checkpoint dir
///      are all legal entries in StorageOptions.AllowedRoots. Validated here.
///   3. Rule output.path_not_allowed surfaces through EncoderRuleId and
///      RuntimeErrors.OutputPathNotAllowed → EncoderErrorShape.Id check.
///   4. Traversal payloads (.., ../../..), symlink escape, UNC path (Windows),
///      device path (\\?\), null-byte injection, Unicode RTL override.
///   5. AcquireLocalPathAsync integration: guard fires before the lease is
///      handed to a ffmpeg child process call site.
/// </summary>
public class AllowlistStressTests
{
    // ── Sub-task 1: regression guard — no raw System.IO bypass ─────────────

    [Fact]
    public void OutputArtifact_MimeFromPath_uses_only_string_manipulation_not_filesystem()
    {
        // Path.GetExtension never touches the disk. This assertion documents
        // that the one file flagged by the grep (OutputArtifact.cs) is safe.
        string mime = OutputArtifact.MimeFromPath(path: "/some/path/video.mp4");
        mime.Should().Be(expected: "video/mp4");

        // Confirm it works on a path that doesn't exist on disk
        string phantom = OutputArtifact.MimeFromPath(path: "/nonexistent/path/that/does/not/exist.m3u8");
        phantom.Should().Be(expected: "application/vnd.apple.mpegurl");
    }

    // ── Sub-task 2: allowlist config coverage ───────────────────────────────

    [Fact]
    public void StorageOptions_accepts_all_canonical_root_types()
    {
        // Every category of root the encoder uses must be registerable.
        string tempBase = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nm-allowlist-config-" + Path.GetRandomFileName()
        );
        string libraryRoot = Path.Combine(path1: tempBase, path2: "library");
        string outputRoot = Path.Combine(path1: tempBase, path2: "output");
        string liveTranscodeCache = Path.Combine(path1: tempBase, path2: "live-transcode");
        string remoteSourceCache = Path.Combine(path1: tempBase, path2: "remote-cache");
        string tesseractDataDir = Path.Combine(path1: tempBase, path2: "tesseract");
        string ripOutput = Path.Combine(path1: tempBase, path2: "rip-output");
        string checkpointDir = Path.Combine(path1: tempBase, path2: "checkpoints");

        StorageOptions opts = new()
        {
            AllowedRoots =
            [
                libraryRoot,
                outputRoot,
                liveTranscodeCache,
                remoteSourceCache,
                tesseractDataDir,
                ripOutput,
                checkpointDir,
            ],
        };

        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: opts.AllowedRoots, driver: driver);

        guard.Enforced.Should().BeTrue();
        guard.AllowedRoots.Should().HaveCount(expected: 7);

        // Paths under each registered root must be accepted
        foreach (string root in opts.AllowedRoots)
        {
            string inner = Path.Combine(path1: root, path2: "sub", path3: "file.bin");
            string validated = guard.Validate(requestedPath: inner);
            validated.Should().Be(expected: Path.GetFullPath(path: inner), because: $"path under {root} should be accepted");
        }
    }

    // ── Sub-task 3: rule surface check ─────────────────────────────────────

    [Fact]
    public void OutputPathNotAllowed_rule_id_is_stable_constant()
    {
        EncoderRuleId.OutputPathNotAllowed.Should().Be(expected: "output.path_not_allowed");
    }

    [Fact]
    public void RuntimeErrors_OutputPathNotAllowed_produces_correct_error_shape()
    {
        EncoderRuntimeException ex = RuntimeErrors.OutputPathNotAllowed(
            path: "/tmp/escaped/../../../etc/passwd",
            reason: "path is not under any allowed root"
        );

        ex.Shape.Id.Should().Be(expected: EncoderRuleId.OutputPathNotAllowed);
        ex.HttpStatusCode.Should().Be(expected: 403);
        ex.Shape.Message.Should().Contain(expected: "path is not under any allowed root");
        ex.Shape.Suggestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StoragePathNotAllowedException_message_contains_rule_id_prefix()
    {
        StoragePathNotAllowedException ex = new(attemptedPath: "/bad/path", reason: "path is not under any allowed root");

        // The exception message must embed the rule ID so log scrapers and
        // the controller error middleware can reliably identify it.
        ex.Message.Should().StartWith(expected: "output.path_not_allowed:");
        ex.Reason.Should().Be(expected: "path is not under any allowed root");
        ex.AttemptedPath.Should().Be(expected: "/bad/path");
    }

    // ── Sub-task 4: traversal + hostile path stress ─────────────────────────

    private static StoragePathGuard EnforcedGuard(string root)
    {
        LocalStorageDriver driver = new();
        return new(allowedRoots: [root], driver: driver);
    }

    [Fact]
    public void Traversal_single_dot_dot_is_rejected()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-trav1"));
        StoragePathGuard guard = EnforcedGuard(root: root);

        string escape = Path.Combine(path1: root, path2: "..", path3: "outside.txt");

        Action act = () => guard.Validate(requestedPath: escape);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e =>
                e.Reason.StartsWith(".. traversal is not allowed")
                || e.Reason.StartsWith("path is not under any allowed root")
            );
    }

    [Fact]
    public void Traversal_deep_triple_dot_dot_is_rejected()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-trav3"));
        StoragePathGuard guard = EnforcedGuard(root: root);

        string escape = Path.Combine(paths: [root, "a", "b", "c", "..", "..", "..", "..", "outside.txt"]);

        Action act = () => guard.Validate(requestedPath: escape);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e =>
                e.Reason.StartsWith(".. traversal is not allowed")
                || e.Reason.StartsWith("path is not under any allowed root")
            );
    }

    [Fact]
    public void Null_byte_injection_is_rejected_before_canonicalization()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-null"));
        StoragePathGuard guard = EnforcedGuard(root: root);

        Action act = () => guard.Validate(requestedPath: root + "/sub\0/evil.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "null byte in path");
    }

    [Fact]
    public void Windows_device_path_question_mark_is_rejected()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-dev"));
        StoragePathGuard guard = EnforcedGuard(root: root);

        Action act = () => guard.Validate(requestedPath: @"\\?\C:\Windows\System32\cmd.exe");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "device paths are not allowed");
    }

    [Fact]
    public void Windows_device_path_dot_is_rejected()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-dev2"));
        StoragePathGuard guard = EnforcedGuard(root: root);

        Action act = () => guard.Validate(requestedPath: @"\\.\PhysicalDrive0");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "device paths are not allowed");
    }

    [Fact]
    public void UNC_escape_rejected_when_root_is_local_path()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-unc"));
        StoragePathGuard guard = EnforcedGuard(root: root);

        Action act = () => guard.Validate(requestedPath: @"\\attacker\share\stolen.mp4");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e =>
                e.Reason.StartsWith("path is not under any allowed root")
                || e.Reason == "device paths are not allowed"
            );
    }

    [Fact]
    public void Symlink_escape_is_rejected_via_mock_backend()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-symlink"));
        string linkInside = Path.Combine(path1: root, path2: "link");
        string realOutside = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-symlink-target", path3: "secret.txt")
        );

        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Strict);
        driver
            .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
        driver.Setup(expression: b => b.ResolveLinkTarget(Path.GetFullPath(linkInside))).Returns(value: realOutside);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);

        Action act = () => guard.Validate(requestedPath: linkInside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Unicode_rtl_override_in_path_is_accepted_when_inside_root()
    {
        // U+202E cannot cause the guard to accept an outside path — the OS
        // canonicalizes using the real bytes, not the visual rendering.
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-rtl"));
        StoragePathGuard guard = EnforcedGuard(root: root);

        string insidePath = Path.Combine(path1: root, path2: "sub", path3: "cod‮exe.txt");
        string result = guard.Validate(requestedPath: insidePath);

        result.Should().Be(expected: Path.GetFullPath(path: insidePath));
    }

    [Fact]
    public void Empty_string_is_rejected_unconditionally()
    {
        StoragePathGuard guard = EnforcedGuard(
            root: Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-empty"))
        );

        Action act = () => guard.Validate(requestedPath: "");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "path is empty");
    }

    [Fact]
    public void Whitespace_only_is_rejected_unconditionally()
    {
        StoragePathGuard guard = EnforcedGuard(
            root: Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-ws"))
        );

        Action act = () => guard.Validate(requestedPath: "   \t  ");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "path is empty");
    }

    // ── Sub-task 5: AcquireLocalPath guard integration ──────────────────────

    [Fact]
    public async Task AcquireLocalPath_fires_guard_and_returns_canonical_path()
    {
        string root = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-lease-" + Path.GetRandomFileName())
        );
        Directory.CreateDirectory(path: root);

        string filePath = Path.Combine(path1: root, path2: "segment.ts");
        File.WriteAllBytes(path: filePath, bytes: [0xAB, 0xCD]);

        try
        {
            LocalStorageDriver driver = new();
            StoragePathGuard guard = new(allowedRoots: [root], driver: driver);
            LocalStorage storage = new(driver: driver, guard: guard);

            await using LocalPathLease lease = storage.AcquireLocalPath(path: filePath);

            lease.Path.Should().Be(expected: Path.GetFullPath(path: filePath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(path: root))
                    Directory.Delete(path: root, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    [Fact]
    public void AcquireLocalPath_rejects_path_outside_allowed_root()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-lease-deny"));
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);

        string outside = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-lease-escape", path3: "stolen.ts")
        );

        Action act = () => storage.AcquireLocalPath(path: outside);

        act.Should().Throw<StoragePathNotAllowedException>();
    }

    [Fact]
    public async Task AcquireLocalPathAsync_rejects_traversal_escape()
    {
        string root = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-lease-async-" + Path.GetRandomFileName())
        );
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);

        string escape = Path.Combine(path1: root, path2: "..", path3: "outside-async.ts");

        Func<Task> act = () => storage.AcquireLocalPathAsync(path: escape, ct: CancellationToken.None);

        await act.Should().ThrowAsync<StoragePathNotAllowedException>();
    }

    [Fact]
    public async Task LoggingStorage_records_AcquireLocalPath_call_before_delegating()
    {
        // Verifies the logging decorator (used in ffmpeg call-site integration
        // tests) correctly intercepts AcquireLocalPath so any un-leased path
        // access is visible in the call log.
        string root = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-logging-" + Path.GetRandomFileName())
        );
        Directory.CreateDirectory(path: root);
        string filePath = Path.Combine(path1: root, path2: "video.ts");
        File.WriteAllBytes(path: filePath, bytes: [0x00]);

        try
        {
            LocalStorageDriver driver = new();
            StoragePathGuard guard = new(allowedRoots: [root], driver: driver);
            LocalStorage inner = new(driver: driver, guard: guard);
            LoggingStorage logger = new(inner: inner);

            await using LocalPathLease lease = logger.AcquireLocalPath(path: filePath);

            logger.Calls.Should().Contain(predicate: c => c.StartsWith("AcquireLocalPath:"));
            lease.Path.Should().Be(expected: Path.GetFullPath(path: filePath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(path: root))
                    Directory.Delete(path: root, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    [Fact]
    public async Task LoggingStorage_records_AcquireLocalPathAsync_call()
    {
        string root = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-stress-logging-async-" + Path.GetRandomFileName())
        );
        Directory.CreateDirectory(path: root);
        string filePath = Path.Combine(path1: root, path2: "segment.ts");
        File.WriteAllBytes(path: filePath, bytes: [0x00]);

        try
        {
            LocalStorageDriver driver = new();
            StoragePathGuard guard = new(allowedRoots: [root], driver: driver);
            LocalStorage inner = new(driver: driver, guard: guard);
            LoggingStorage logger = new(inner: inner);

            await using LocalPathLease lease = await logger.AcquireLocalPathAsync(
                path: filePath,
                ct: CancellationToken.None
            );

            logger.Calls.Should().Contain(predicate: c => c.StartsWith("AcquireLocalPathAsync:"));
            lease.Path.Should().Be(expected: Path.GetFullPath(path: filePath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(path: root))
                    Directory.Delete(path: root, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
