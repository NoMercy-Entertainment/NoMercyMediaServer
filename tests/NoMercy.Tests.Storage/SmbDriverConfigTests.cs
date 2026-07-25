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
[Trait("Category", "Unit")]
public sealed class SmbDriverConfigTests
{
    private static readonly Ulid FolderId = Ulid.NewUlid();

    [Fact]
    public void Parse_minimal_valid_json_produces_defaults()
    {
        SmbDriverConfig config = SmbDriverConfig.Parse(
            """{"host":"nas.local","share":"media"}""",
            FolderId
        );

        config.Host.Should().Be("nas.local");
        config.Share.Should().Be("media");
        config.Port.Should().Be(445, "445 is the standard SMB port and must be the default");
        config.TimeoutSeconds.Should().Be(30, "30s must be the default timeout");
        config.Domain.Should().Be(string.Empty);
        config.BasePath.Should().Be(string.Empty);
        config
            .Username.Should()
            .BeNull("credentials come from the credential store, never the JSON blob");
        config.Password.Should().BeNull();
    }

    [Fact]
    public void Parse_trims_host_and_normalizes_share_and_path()
    {
        SmbDriverConfig config = SmbDriverConfig.Parse(
            """{"host":"  nas.local  ","share":"/media/","path":"\\movies\\4k\\","domain":" WORKGROUP "}""",
            FolderId
        );

        config.Host.Should().Be("nas.local", "leading/trailing whitespace must be trimmed");
        config
            .Share.Should()
            .Be("media", "leading/trailing slashes must be stripped from the share name");
        config
            .BasePath.Should()
            .Be("movies/4k", "backslashes must be normalized to forward slashes and trimmed");
        config.Domain.Should().Be("WORKGROUP", "domain must be trimmed");
    }

    [Fact]
    public void Parse_missing_host_throws()
    {
        Action act = () => SmbDriverConfig.Parse("""{"share":"media"}""", FolderId);

        act.Should()
            .Throw<ArgumentException>("host is required to know which server to connect to")
            .WithMessage("*host*");
    }

    [Theory]
    [InlineData("""{"host":"   ","share":"media"}""")]
    [InlineData("""{"host":"","share":"media"}""")]
    public void Parse_blank_host_throws(string json)
    {
        Action act = () => SmbDriverConfig.Parse(json, FolderId);

        act.Should().Throw<ArgumentException>().WithMessage("*host*");
    }

    [Fact]
    public void Parse_missing_share_throws()
    {
        Action act = () => SmbDriverConfig.Parse("""{"host":"nas.local"}""", FolderId);

        act.Should()
            .Throw<ArgumentException>("share is required to know which SMB share to mount")
            .WithMessage("*share*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void Parse_port_out_of_range_throws(int port)
    {
        string json = $$"""{"host":"nas.local","share":"media","port":{{port}}}""";

        Action act = () => SmbDriverConfig.Parse(json, FolderId);

        act.Should()
            .Throw<ArgumentException>($"port {port} is outside the valid TCP port range")
            .WithMessage("*port*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(445)]
    [InlineData(65535)]
    public void Parse_port_at_valid_boundaries_succeeds(int port)
    {
        string json = $$"""{"host":"nas.local","share":"media","port":{{port}}}""";

        SmbDriverConfig config = SmbDriverConfig.Parse(json, FolderId);

        config.Port.Should().Be(port);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Parse_non_positive_timeout_throws(int timeout)
    {
        string json = $$"""{"host":"nas.local","share":"media","timeoutSeconds":{{timeout}}}""";

        Action act = () => SmbDriverConfig.Parse(json, FolderId);

        act.Should()
            .Throw<ArgumentException>(
                "a zero or negative timeout would make every operation fail instantly"
            )
            .WithMessage("*timeoutSeconds*");
    }

    [Fact]
    public void Parse_malformed_json_throws_ArgumentException_not_JsonException()
    {
        // Callers (SmbDriverBuilder) catch ArgumentException for all config
        // problems; a raw JsonException escaping would bypass that handling.
        Action act = () => SmbDriverConfig.Parse("{not valid json", FolderId);

        act.Should()
            .Throw<ArgumentException>()
            .WithInnerException<System.Text.Json.JsonException>();
    }

    [Fact]
    public void Parse_json_null_literal_throws()
    {
        Action act = () => SmbDriverConfig.Parse("null", FolderId);

        act.Should().Throw<ArgumentException>().WithMessage("*null*");
    }

    [Fact]
    public void Parse_is_case_insensitive_for_property_names()
    {
        SmbDriverConfig config = SmbDriverConfig.Parse(
            """{"HOST":"nas.local","Share":"media","PORT":1234}""",
            FolderId
        );

        config.Host.Should().Be("nas.local");
        config.Share.Should().Be("media");
        config.Port.Should().Be(1234);
    }

    [Fact]
    public void For_overload_builds_config_without_json()
    {
        SmbDriverConfig config = SmbDriverConfig.For(
            "nas.local",
            "/media/",
            username: "alice",
            password: "secret",
            domain: "WORKGROUP",
            basePath: @"\movies\",
            port: 139,
            timeoutSeconds: 15
        );

        config.Host.Should().Be("nas.local");
        config.Share.Should().Be("media", "share must be normalized just like the JSON path");
        config.Username.Should().Be("alice");
        config.Password.Should().Be("secret");
        config.Domain.Should().Be("WORKGROUP");
        config.BasePath.Should().Be("movies");
        config.Port.Should().Be(139);
        config.TimeoutSeconds.Should().Be(15);
    }

    [Fact]
    public void For_overload_uses_defaults_when_optional_args_omitted()
    {
        SmbDriverConfig config = SmbDriverConfig.For("nas.local", "media");

        config.Username.Should().BeNull();
        config.Password.Should().BeNull();
        config.Domain.Should().Be(string.Empty);
        config.BasePath.Should().Be(string.Empty);
        config.Port.Should().Be(445);
        config.TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void With_expression_replaces_credentials_without_mutating_original()
    {
        // Regression: SmbDriverBuilder does `config = config with { Username = ..., Password = ... }`
        // after credential resolution. A record without proper value semantics
        // would corrupt the original config or silently share state.
        SmbDriverConfig original = SmbDriverConfig.For("nas.local", "media");

        SmbDriverConfig withCreds = original with { Username = "bob", Password = "hunter2" };

        original
            .Username.Should()
            .BeNull("the `with` expression must not mutate the original record");
        withCreds.Username.Should().Be("bob");
        withCreds.Host.Should().Be(original.Host, "unrelated fields must carry over unchanged");
    }
}
