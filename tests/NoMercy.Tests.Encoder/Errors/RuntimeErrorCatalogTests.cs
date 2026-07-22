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

using System.Reflection;
using NoMercy.Encoder.Errors;

namespace NoMercy.Tests.Encoder.Errors;

/// <summary>
/// Generator test — Phase 4.19 of encoder-v3-alignment.md.
///
/// Asserts that:
///   1. Every const on <see cref="EncoderRuleId"/> exists on
///      <see cref="EncoderRuntimeErrorId"/> (if it is a runtime error ID).
///   2. Every const on <see cref="EncoderRuntimeErrorId"/> exists on
///      <see cref="EncoderRuleId"/> (alias integrity check).
///   3. Every const on both classes has a matching entry in
///      <c>docs/encoder-errors.md</c> in the repository root.
/// </summary>
public class RuntimeErrorCatalogTests
{
    private static readonly string[] RuntimeErrorIds = typeof(EncoderRuntimeErrorId)
        .GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(predicate: f => f is { IsLiteral: true, IsInitOnly: false })
        .Select(selector: f => (string)f.GetValue(obj: null)!)
        .ToArray();

    private static readonly string[] AllRuleIds = typeof(EncoderRuleId)
        .GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(predicate: f => f is { IsLiteral: true, IsInitOnly: false })
        .Select(selector: f => (string)f.GetValue(obj: null)!)
        .ToArray();

    // ---- helper: locate docs/encoder-errors.md relative to the solution root ----

    private static string FindDocsFile()
    {
        // Walk up from the test assembly until we find the docs directory.
        DirectoryInfo? dir = new FileInfo(
            fileName: typeof(RuntimeErrorCatalogTests).Assembly.Location
        ).Directory;

        while (dir is not null)
        {
            string candidate = Path.Combine(path1: dir.FullName, path2: "docs", path3: "encoder-errors.md");
            if (File.Exists(path: candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            message: "Could not locate docs/encoder-errors.md. "
                     + "Expected it at the repository root next to apps/, docs/, infra/ etc."
        );
    }

    // ---- 1. EncoderRuntimeErrorId values are all present in EncoderRuleId ------

    [Fact]
    public void Every_runtime_error_constant_is_aliased_from_EncoderRuleId()
    {
        foreach (string id in RuntimeErrorIds)
        {
            AllRuleIds
                .Should()
                .Contain(
                    expected: id,
                    because: $"EncoderRuntimeErrorId.{id} must be an alias of an EncoderRuleId constant"
                );
        }
    }

    // ---- 2. EncoderRuntimeErrorId covers the required 21 runtime error IDs -----

    [Theory]
    [InlineData(data: "hardware.forced_but_unavailable")]
    [InlineData(data: "hardware.gpu_telemetry_unsupported")]
    [InlineData(data: "gpu_capacity_exhausted")]
    [InlineData(data: "encoder.init_failed")]
    [InlineData(data: "source.not_accessible")]
    [InlineData(data: "source.read_error")]
    [InlineData(data: "output.write_error")]
    [InlineData(data: "output.path_not_allowed")]
    [InlineData(data: "license.revoked")]
    [InlineData(data: "license.unreachable")]
    [InlineData(data: "capability.fpcalc_missing")]
    [InlineData(data: "capability.whisper_missing")]
    [InlineData(data: "capability.tesseract_model_missing")]
    [InlineData(data: "disc.drive_busy")]
    [InlineData(data: "disc.aacs_cert_missing")]
    [InlineData(data: "disc.bdplus_converter_missing")]
    [InlineData(data: "disc.read_error")]
    [InlineData(data: "job.interrupted_no_checkpoint")]
    [InlineData(data: "distribution.hmac_invalid")]
    [InlineData(data: "distribution.timestamp_replay")]
    [InlineData(data: "distribution.worker_not_registered")]
    public void Required_runtime_error_id_is_present(string expectedId)
    {
        RuntimeErrorIds
            .Should()
            .Contain(
                expected: expectedId,
                because: $"EncoderRuntimeErrorId must declare the required runtime error ID '{expectedId}'"
            );
    }

    // ---- 3. Every EncoderRuleId constant has a docs entry ----------------------

    [Fact]
    public void Every_EncoderRuleId_constant_has_a_docs_entry()
    {
        string docsFile = FindDocsFile();
        string docsContent = File.ReadAllText(path: docsFile);

        List<string> missing = AllRuleIds.Where(predicate: id => !docsContent.Contains(value: $"`{id}`")).ToList();

        missing
            .Should()
            .BeEmpty(
                because: "The following EncoderRuleId constants are missing from docs/encoder-errors.md: "
                         + string.Join(separator: ", ", values: missing)
            );
    }

    // ---- 4. Every EncoderRuntimeErrorId constant has a docs entry --------------

    [Fact]
    public void Every_EncoderRuntimeErrorId_constant_has_a_docs_entry()
    {
        string docsFile = FindDocsFile();
        string docsContent = File.ReadAllText(path: docsFile);

        List<string> missing = RuntimeErrorIds
            .Where(predicate: id => !docsContent.Contains(value: $"`{id}`"))
            .ToList();

        missing
            .Should()
            .BeEmpty(
                because: "The following EncoderRuntimeErrorId constants are missing from docs/encoder-errors.md: "
                         + string.Join(separator: ", ", values: missing)
            );
    }

    // ---- 5. Constant count guard — catches accidental deletions ----------------

    [Fact]
    public void EncoderRuntimeErrorId_declares_exactly_21_constants()
    {
        RuntimeErrorIds.Should().HaveCount(expected: 21);
    }
}
