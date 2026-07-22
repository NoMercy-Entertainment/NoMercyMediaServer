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

using NoMercy.Encoder.Analysis;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait(name: "Category", value: "Unit")]
public class DiscInfoTests
{
    private static DiscTitle MakeTitle(int index, TimeSpan duration, bool isMainFeature = false) =>
        new(
            Index: index,
            Name: $"Title {index}",
            Duration: duration,
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: [],
            EstimatedSizeBytes: 0,
            IsMainFeature: isMainFeature
        );

    [Fact]
    public void MainTitleDurationSec_SingleTitle_ReturnsDurationInSeconds()
    {
        DiscTitle title = MakeTitle(index: 0, duration: TimeSpan.FromSeconds(seconds: 3661));
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [title], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 3661);
    }

    [Fact]
    public void MainTitleDurationSec_MultipleUnflaggedTitles_ReturnsDurationOfLongest()
    {
        DiscTitle title1 = MakeTitle(index: 0, duration: TimeSpan.FromSeconds(seconds: 1800));
        DiscTitle title2 = MakeTitle(index: 1, duration: TimeSpan.FromSeconds(seconds: 5400));
        DiscTitle title3 = MakeTitle(index: 2, duration: TimeSpan.FromSeconds(seconds: 3600));

        DiscInfo disc = new(
            Type: OpticalDiscType.Dvd,
            DiscLabel: "TEST",
            Titles: [title1, title2, title3],
            AudioTracks: null,
            TotalDuration: TimeSpan.Zero
        );

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 5400);
    }

    [Fact]
    public void MainTitleDurationSec_FlaggedMainFeature_PrefersFlaggedOverLongest()
    {
        DiscTitle title1 = MakeTitle(index: 0, duration: TimeSpan.FromSeconds(seconds: 3600), isMainFeature: true);
        DiscTitle title2 = MakeTitle(index: 1, duration: TimeSpan.FromSeconds(seconds: 5400), isMainFeature: false);

        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [title1, title2], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 3600);
    }

    [Fact]
    public void MainTitleDurationSec_NoTitles_ReturnsZero()
    {
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 0);
    }

    [Fact]
    public void MainTitleDurationSec_TitleWithZeroDuration_ReturnsZero()
    {
        DiscTitle title = MakeTitle(index: 0, duration: TimeSpan.Zero);
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [title], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 0);
    }

    [Fact]
    public void MainTitleDurationSec_RoundsTruncatesDecimalSeconds()
    {
        DiscTitle title = MakeTitle(index: 0, duration: TimeSpan.FromSeconds(value: 100.9));
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [title], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 100);
    }

    [Fact]
    public void MainTitleDurationSec_LargeDurations_Handled()
    {
        DiscTitle title = MakeTitle(index: 0, duration: TimeSpan.FromHours(hours: 10));
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [title], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 36000);
    }

    [Fact]
    public void MainTitleDurationSec_PartialSeconds_Converted()
    {
        DiscTitle title = MakeTitle(index: 0, duration: TimeSpan.FromSeconds(value: 3.5));
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [title], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 3);
    }

    [Fact]
    public void MainTitleDurationSec_MultipleTitles_OnlyMainFeatureFlaggedButShorter()
    {
        DiscTitle mainFeature = MakeTitle(index: 0, duration: TimeSpan.FromSeconds(seconds: 1800), isMainFeature: true);
        DiscTitle bonus1 = MakeTitle(index: 1, duration: TimeSpan.FromSeconds(seconds: 3600), isMainFeature: false);
        DiscTitle bonus2 = MakeTitle(index: 2, duration: TimeSpan.FromSeconds(seconds: 2700), isMainFeature: false);

        DiscInfo disc = new(
            Type: OpticalDiscType.Dvd,
            DiscLabel: "TEST",
            Titles: [mainFeature, bonus1, bonus2],
            AudioTracks: null,
            TotalDuration: TimeSpan.Zero
        );

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 1800);
    }

    [Fact]
    public void MainTitleDurationSec_FirstTitleIsMain_ReturnsItsDuration()
    {
        DiscTitle main = MakeTitle(index: 0, duration: TimeSpan.FromSeconds(seconds: 7200), isMainFeature: true);
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [main], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        int result = disc.MainTitleDurationSec;

        result.Should().Be(expected: 7200);
    }

    [Fact]
    public void Type_StoresDiscType()
    {
        DiscInfo disc = new(Type: OpticalDiscType.BluRay, DiscLabel: "TEST", Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        disc.Type.Should().Be(expected: OpticalDiscType.BluRay);
    }

    [Fact]
    public void DiscLabel_StoresLabel()
    {
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "MY_DISC", Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        disc.DiscLabel.Should().Be(expected: "MY_DISC");
    }

    [Fact]
    public void Titles_StoresTitleArray()
    {
        DiscTitle title = MakeTitle(index: 0, duration: TimeSpan.FromSeconds(seconds: 3600));
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [title], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        disc.Titles.Should().HaveCount(expected: 1);
        disc.Titles[0].Should().Be(expected: title);
    }

    [Fact]
    public void TotalDuration_StoresTotalDuration()
    {
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [], AudioTracks: null, TotalDuration: TimeSpan.FromHours(hours: 2));

        disc.TotalDuration.Should().Be(expected: TimeSpan.FromHours(hours: 2));
    }

    [Fact]
    public void DiscTitle_Stores_EmbeddedTitle()
    {
        DiscInfo disc = new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: "VOLUME_LABEL",
            Titles: [],
            AudioTracks: null,
            TotalDuration: TimeSpan.Zero,
            DiscTitle: "The Dark Knight"
        );

        disc.DiscTitle.Should().Be(expected: "The Dark Knight");
    }

    [Fact]
    public void Protection_StoresProtectionInfo()
    {
        DiscProtection protection = new(Kind: "AACS", VolumeId: "ABC123", Message: "AACS protected");
        DiscInfo disc = new(Type: OpticalDiscType.BluRay, DiscLabel: "TEST", Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero, Protection: protection);

        disc.Protection.Should().Be(expected: protection);
    }

    [Fact]
    public void Protection_Null_WhenNotProvided()
    {
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        disc.Protection.Should().BeNull();
    }

    [Fact]
    public void AudioTracks_StoresAudioTracks()
    {
        DiscTrack track = new(Index: 0, Title: "Track 1", Artist: "Artist", Duration: TimeSpan.FromSeconds(seconds: 180), SampleRate: 44100, Channels: 2);
        DiscInfo disc = new(Type: OpticalDiscType.Cd, DiscLabel: "AUDIO_CD", Titles: [], AudioTracks: [track], TotalDuration: TimeSpan.Zero);

        disc.AudioTracks.Should().HaveCount(expected: 1);
        disc.AudioTracks![0].Should().Be(expected: track);
    }

    [Fact]
    public void AudioTracks_Null_ForVideoDiscs()
    {
        DiscInfo disc = new(Type: OpticalDiscType.Dvd, DiscLabel: "TEST", Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);

        disc.AudioTracks.Should().BeNull();
    }
}
