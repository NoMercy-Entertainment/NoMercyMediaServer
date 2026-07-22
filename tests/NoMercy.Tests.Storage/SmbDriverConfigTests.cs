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

using NoMercy.Storage.Drivers.Smb;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="SmbDriverConfig.Parse"/> is the only line of defense between a
/// malformed <c>Folder.DriverConfig</c> JSON blob (typed by hand in the
/// dashboard, or migrated from an older schema) and a driver that connects
/// with the wrong port, no share, or a garbage timeout. Every validation
/// branch is demanded here — a passing test that doesn't also fail when the
/// branch is deleted is not a real requirement test.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class SmbDriverConfigTests
{
    private static readonly Ulid FolderId = Ulid.NewUlid();

    [Fact]
    public void Parse_minimal_valid_json_produces_defaults()
    {
        SmbDriverConfig config = SmbDriverConfig.Parse(
            json: """{"host":"nas.local","share":"media"}""",
            folderId: FolderId
        );

        config.Host.Should().Be(expected: "nas.local");
        config.Share.Should().Be(expected: "media");
        config.Port.Should().Be(expected: 445, because: "445 is the standard SMB port and must be the default");
        config.TimeoutSeconds.Should().Be(expected: 30, because: "30s must be the default timeout");
        config.Domain.Should().Be(expected: string.Empty);
        config.BasePath.Should().Be(expected: string.Empty);
        config
            .Username.Should()
            .BeNull(because: "credentials come from the credential store, never the JSON blob");
        config.Password.Should().BeNull();
    }

    [Fact]
    public void Parse_trims_host_and_normalizes_share_and_path()
    {
        SmbDriverConfig config = SmbDriverConfig.Parse(
            json: """{"host":"  nas.local  ","share":"/media/","path":"\\movies\\4k\\","domain":" WORKGROUP "}""",
            folderId: FolderId
        );

        config.Host.Should().Be(expected: "nas.local", because: "leading/trailing whitespace must be trimmed");
        config
            .Share.Should()
            .Be(expected: "media", because: "leading/trailing slashes must be stripped from the share name");
        config
            .BasePath.Should()
            .Be(expected: "movies/4k", because: "backslashes must be normalized to forward slashes and trimmed");
        config.Domain.Should().Be(expected: "WORKGROUP", because: "domain must be trimmed");
    }

    [Fact]
    public void Parse_missing_host_throws()
    {
        Action act = () => SmbDriverConfig.Parse(json: """{"share":"media"}""", folderId: FolderId);

        act.Should()
            .Throw<ArgumentException>(because: "host is required to know which server to connect to")
            .WithMessage(expectedWildcardPattern: "*host*");
    }

    [Theory]
    [InlineData(data: """{"host":"   ","share":"media"}""")]
    [InlineData(data: """{"host":"","share":"media"}""")]
    public void Parse_blank_host_throws(string json)
    {
        Action act = () => SmbDriverConfig.Parse(json: json, folderId: FolderId);

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*host*");
    }

    [Fact]
    public void Parse_missing_share_throws()
    {
        Action act = () => SmbDriverConfig.Parse(json: """{"host":"nas.local"}""", folderId: FolderId);

        act.Should()
            .Throw<ArgumentException>(because: "share is required to know which SMB share to mount")
            .WithMessage(expectedWildcardPattern: "*share*");
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: -1)]
    [InlineData(data: 65536)]
    [InlineData(data: 100000)]
    public void Parse_port_out_of_range_throws(int port)
    {
        string json = $$"""{"host":"nas.local","share":"media","port":{{port}}}""";

        Action act = () => SmbDriverConfig.Parse(json: json, folderId: FolderId);

        act.Should()
            .Throw<ArgumentException>(because: $"port {port} is outside the valid TCP port range")
            .WithMessage(expectedWildcardPattern: "*port*");
    }

    [Theory]
    [InlineData(data: 1)]
    [InlineData(data: 445)]
    [InlineData(data: 65535)]
    public void Parse_port_at_valid_boundaries_succeeds(int port)
    {
        string json = $$"""{"host":"nas.local","share":"media","port":{{port}}}""";

        SmbDriverConfig config = SmbDriverConfig.Parse(json: json, folderId: FolderId);

        config.Port.Should().Be(expected: port);
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: -5)]
    public void Parse_non_positive_timeout_throws(int timeout)
    {
        string json = $$"""{"host":"nas.local","share":"media","timeoutSeconds":{{timeout}}}""";

        Action act = () => SmbDriverConfig.Parse(json: json, folderId: FolderId);

        act.Should()
            .Throw<ArgumentException>(
                because: "a zero or negative timeout would make every operation fail instantly"
            )
            .WithMessage(expectedWildcardPattern: "*timeoutSeconds*");
    }

    [Fact]
    public void Parse_malformed_json_throws_ArgumentException_not_JsonException()
    {
        // Callers (SmbDriverBuilder) catch ArgumentException for all config
        // problems; a raw JsonException escaping would bypass that handling.
        Action act = () => SmbDriverConfig.Parse(json: "{not valid json", folderId: FolderId);

        act.Should()
            .Throw<ArgumentException>()
            .WithInnerException<System.Text.Json.JsonException>();
    }

    [Fact]
    public void Parse_json_null_literal_throws()
    {
        Action act = () => SmbDriverConfig.Parse(json: "null", folderId: FolderId);

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*null*");
    }

    [Fact]
    public void Parse_is_case_insensitive_for_property_names()
    {
        SmbDriverConfig config = SmbDriverConfig.Parse(
            json: """{"HOST":"nas.local","Share":"media","PORT":1234}""",
            folderId: FolderId
        );

        config.Host.Should().Be(expected: "nas.local");
        config.Share.Should().Be(expected: "media");
        config.Port.Should().Be(expected: 1234);
    }

    [Fact]
    public void For_overload_builds_config_without_json()
    {
        SmbDriverConfig config = SmbDriverConfig.For(
            host: "nas.local",
            share: "/media/",
            username: "alice",
            password: "secret",
            domain: "WORKGROUP",
            basePath: @"\movies\",
            port: 139,
            timeoutSeconds: 15
        );

        config.Host.Should().Be(expected: "nas.local");
        config.Share.Should().Be(expected: "media", because: "share must be normalized just like the JSON path");
        config.Username.Should().Be(expected: "alice");
        config.Password.Should().Be(expected: "secret");
        config.Domain.Should().Be(expected: "WORKGROUP");
        config.BasePath.Should().Be(expected: "movies");
        config.Port.Should().Be(expected: 139);
        config.TimeoutSeconds.Should().Be(expected: 15);
    }

    [Fact]
    public void For_overload_uses_defaults_when_optional_args_omitted()
    {
        SmbDriverConfig config = SmbDriverConfig.For(host: "nas.local", share: "media");

        config.Username.Should().BeNull();
        config.Password.Should().BeNull();
        config.Domain.Should().Be(expected: string.Empty);
        config.BasePath.Should().Be(expected: string.Empty);
        config.Port.Should().Be(expected: 445);
        config.TimeoutSeconds.Should().Be(expected: 30);
    }

    [Fact]
    public void With_expression_replaces_credentials_without_mutating_original()
    {
        // Regression: SmbDriverBuilder does `config = config with { Username = ..., Password = ... }`
        // after credential resolution. A record without proper value semantics
        // would corrupt the original config or silently share state.
        SmbDriverConfig original = SmbDriverConfig.For(host: "nas.local", share: "media");

        SmbDriverConfig withCreds = original with { Username = "bob", Password = "hunter2" };

        original
            .Username.Should()
            .BeNull(because: "the `with` expression must not mutate the original record");
        withCreds.Username.Should().Be(expected: "bob");
        withCreds.Host.Should().Be(expected: original.Host, because: "unrelated fields must carry over unchanged");
    }
}
