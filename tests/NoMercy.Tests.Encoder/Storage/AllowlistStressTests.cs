namespace NoMercy.Tests.Encoder.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Storage;

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
        string mime = NoMercy.Encoder.Output.OutputArtifact.MimeFromPath("/some/path/video.mp4");
        mime.Should().Be("video/mp4");

        // Confirm it works on a path that doesn't exist on disk
        string phantom = NoMercy.Encoder.Output.OutputArtifact.MimeFromPath(
            "/nonexistent/path/that/does/not/exist.m3u8"
        );
        phantom.Should().Be("application/vnd.apple.mpegurl");
    }

    // ── Sub-task 2: allowlist config coverage ───────────────────────────────

    [Fact]
    public void StorageOptions_accepts_all_canonical_root_types()
    {
        // Every category of root the encoder uses must be registerable.
        string tempBase = Path.Combine(
            Path.GetTempPath(),
            "nm-allowlist-config-" + Path.GetRandomFileName()
        );
        string libraryRoot = Path.Combine(tempBase, "library");
        string outputRoot = Path.Combine(tempBase, "output");
        string liveTranscodeCache = Path.Combine(tempBase, "live-transcode");
        string remoteSourceCache = Path.Combine(tempBase, "remote-cache");
        string tesseractDataDir = Path.Combine(tempBase, "tesseract");
        string ripOutput = Path.Combine(tempBase, "rip-output");
        string checkpointDir = Path.Combine(tempBase, "checkpoints");

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

        SystemIoStorageBackend backend = new();
        StoragePathGuard guard = new(opts.AllowedRoots, backend);

        guard.Enforced.Should().BeTrue();
        guard.AllowedRoots.Should().HaveCount(7);

        // Paths under each registered root must be accepted
        foreach (string root in opts.AllowedRoots)
        {
            string inner = Path.Combine(root, "sub", "file.bin");
            string validated = guard.Validate(inner);
            validated.Should().Be(Path.GetFullPath(inner), $"path under {root} should be accepted");
        }
    }

    // ── Sub-task 3: rule surface check ─────────────────────────────────────

    [Fact]
    public void OutputPathNotAllowed_rule_id_is_stable_constant()
    {
        EncoderRuleId.OutputPathNotAllowed.Should().Be("output.path_not_allowed");
    }

    [Fact]
    public void RuntimeErrors_OutputPathNotAllowed_produces_correct_error_shape()
    {
        EncoderRuntimeException ex = RuntimeErrors.OutputPathNotAllowed(
            "/tmp/escaped/../../../etc/passwd",
            "path is not under any allowed root"
        );

        ex.Shape.Id.Should().Be(EncoderRuleId.OutputPathNotAllowed);
        ex.HttpStatusCode.Should().Be(403);
        ex.Shape.Message.Should().Contain("path is not under any allowed root");
        ex.Shape.Suggestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StoragePathNotAllowedException_message_contains_rule_id_prefix()
    {
        StoragePathNotAllowedException ex = new("/bad/path", "path is not under any allowed root");

        // The exception message must embed the rule ID so log scrapers and
        // the controller error middleware can reliably identify it.
        ex.Message.Should().StartWith("output.path_not_allowed:");
        ex.Reason.Should().Be("path is not under any allowed root");
        ex.AttemptedPath.Should().Be("/bad/path");
    }

    // ── Sub-task 4: traversal + hostile path stress ─────────────────────────

    private static StoragePathGuard EnforcedGuard(string root)
    {
        SystemIoStorageBackend backend = new();
        return new StoragePathGuard([root], backend);
    }

    [Fact]
    public void Traversal_single_dot_dot_is_rejected()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-trav1"));
        StoragePathGuard guard = EnforcedGuard(root);

        string escape = Path.Combine(root, "..", "outside.txt");

        Action act = () => guard.Validate(escape);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Traversal_deep_triple_dot_dot_is_rejected()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-trav3"));
        StoragePathGuard guard = EnforcedGuard(root);

        string escape = Path.Combine(root, "a", "b", "c", "..", "..", "..", "..", "outside.txt");

        Action act = () => guard.Validate(escape);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Null_byte_injection_is_rejected_before_canonicalization()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-null"));
        StoragePathGuard guard = EnforcedGuard(root);

        Action act = () => guard.Validate(root + "/sub\0/evil.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "null byte in path");
    }

    [Fact]
    public void Windows_device_path_question_mark_is_rejected()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-dev"));
        StoragePathGuard guard = EnforcedGuard(root);

        Action act = () => guard.Validate(@"\\?\C:\Windows\System32\cmd.exe");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "device paths are not allowed");
    }

    [Fact]
    public void Windows_device_path_dot_is_rejected()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-dev2"));
        StoragePathGuard guard = EnforcedGuard(root);

        Action act = () => guard.Validate(@"\\.\PhysicalDrive0");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "device paths are not allowed");
    }

    [Fact]
    public void UNC_escape_rejected_when_root_is_local_path()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-unc"));
        StoragePathGuard guard = EnforcedGuard(root);

        Action act = () => guard.Validate(@"\\attacker\share\stolen.mp4");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e =>
                e.Reason.StartsWith("path is not under any allowed root")
                || e.Reason == "device paths are not allowed"
            );
    }

    [Fact]
    public void Symlink_escape_is_rejected_via_mock_backend()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-symlink"));
        string linkInside = Path.Combine(root, "link");
        string realOutside = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "nm-stress-symlink-target", "secret.txt")
        );

        Mock<IStorageBackend> backend = new(MockBehavior.Strict);
        backend
            .Setup(b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(p => Path.GetFullPath(p));
        backend.Setup(b => b.ResolveLinkTarget(Path.GetFullPath(linkInside))).Returns(realOutside);

        StoragePathGuard guard = new([root], backend.Object);

        Action act = () => guard.Validate(linkInside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Unicode_rtl_override_in_path_is_accepted_when_inside_root()
    {
        // U+202E cannot cause the guard to accept an outside path — the OS
        // canonicalizes using the real bytes, not the visual rendering.
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-rtl"));
        StoragePathGuard guard = EnforcedGuard(root);

        string insidePath = Path.Combine(root, "sub", "cod‮exe.txt");
        string result = guard.Validate(insidePath);

        result.Should().Be(Path.GetFullPath(insidePath));
    }

    [Fact]
    public void Empty_string_is_rejected_unconditionally()
    {
        StoragePathGuard guard = EnforcedGuard(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-empty"))
        );

        Action act = () => guard.Validate("");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "path is empty");
    }

    [Fact]
    public void Whitespace_only_is_rejected_unconditionally()
    {
        StoragePathGuard guard = EnforcedGuard(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-ws"))
        );

        Action act = () => guard.Validate("   \t  ");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "path is empty");
    }

    // ── Sub-task 5: AcquireLocalPath guard integration ──────────────────────

    [Fact]
    public async Task AcquireLocalPath_fires_guard_and_returns_canonical_path()
    {
        string root = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "nm-stress-lease-" + Path.GetRandomFileName())
        );
        Directory.CreateDirectory(root);

        string filePath = Path.Combine(root, "segment.ts");
        File.WriteAllBytes(filePath, [0xAB, 0xCD]);

        try
        {
            SystemIoStorageBackend backend = new();
            StoragePathGuard guard = new([root], backend);
            LocalStorage storage = new(backend, guard);

            await using LocalPathLease lease = storage.AcquireLocalPath(filePath);

            lease.Path.Should().Be(Path.GetFullPath(filePath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
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
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-stress-lease-deny"));
        SystemIoStorageBackend backend = new();
        StoragePathGuard guard = new([root], backend);
        LocalStorage storage = new(backend, guard);

        string outside = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "nm-stress-lease-escape", "stolen.ts")
        );

        Action act = () => storage.AcquireLocalPath(outside);

        act.Should().Throw<StoragePathNotAllowedException>();
    }

    [Fact]
    public async Task AcquireLocalPathAsync_rejects_traversal_escape()
    {
        string root = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "nm-stress-lease-async-" + Path.GetRandomFileName())
        );
        SystemIoStorageBackend backend = new();
        StoragePathGuard guard = new([root], backend);
        LocalStorage storage = new(backend, guard);

        string escape = Path.Combine(root, "..", "outside-async.ts");

        Func<Task> act = () => storage.AcquireLocalPathAsync(escape, CancellationToken.None);

        await act.Should().ThrowAsync<StoragePathNotAllowedException>();
    }

    [Fact]
    public async Task LoggingStorage_records_AcquireLocalPath_call_before_delegating()
    {
        // Verifies the logging decorator (used in ffmpeg call-site integration
        // tests) correctly intercepts AcquireLocalPath so any un-leased path
        // access is visible in the call log.
        string root = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "nm-stress-logging-" + Path.GetRandomFileName())
        );
        Directory.CreateDirectory(root);
        string filePath = Path.Combine(root, "video.ts");
        File.WriteAllBytes(filePath, [0x00]);

        try
        {
            SystemIoStorageBackend backend = new();
            StoragePathGuard guard = new([root], backend);
            LocalStorage inner = new(backend, guard);
            LoggingStorage logger = new(inner);

            await using LocalPathLease lease = logger.AcquireLocalPath(filePath);

            logger.Calls.Should().Contain(c => c.StartsWith("AcquireLocalPath:"));
            lease.Path.Should().Be(Path.GetFullPath(filePath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
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
            Path.Combine(Path.GetTempPath(), "nm-stress-logging-async-" + Path.GetRandomFileName())
        );
        Directory.CreateDirectory(root);
        string filePath = Path.Combine(root, "segment.ts");
        File.WriteAllBytes(filePath, [0x00]);

        try
        {
            SystemIoStorageBackend backend = new();
            StoragePathGuard guard = new([root], backend);
            LocalStorage inner = new(backend, guard);
            LoggingStorage logger = new(inner);

            await using LocalPathLease lease = await logger.AcquireLocalPathAsync(
                filePath,
                CancellationToken.None
            );

            logger.Calls.Should().Contain(c => c.StartsWith("AcquireLocalPathAsync:"));
            lease.Path.Should().Be(Path.GetFullPath(filePath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
