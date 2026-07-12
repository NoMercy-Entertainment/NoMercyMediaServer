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

using NoMercy.MediaProcessing.Intake;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Tests.MediaProcessing.Intake;

[Trait("Category", "Unit")]
public class IntakeSettingsTests
{
    [Fact]
    public async Task GetDropFolderAsync_ReturnsNull_WhenUnset()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        string? dropFolder = await settings.GetDropFolderAsync(CancellationToken.None);

        dropFolder.Should().BeNull();
    }

    [Fact]
    public async Task SetDropFolderAsync_RoundTrips()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        await settings.SetDropFolderAsync("/mnt/drop", CancellationToken.None);
        string? dropFolder = await settings.GetDropFolderAsync(CancellationToken.None);

        dropFolder.Should().Be("/mnt/drop");
    }

    [Fact]
    public async Task SetDropFolderAsync_Null_ClearsStoredValue()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        await settings.SetDropFolderAsync("/mnt/drop", CancellationToken.None);
        await settings.SetDropFolderAsync(null, CancellationToken.None);
        string? dropFolder = await settings.GetDropFolderAsync(CancellationToken.None);

        dropFolder.Should().BeNull();
    }

    [Fact]
    public async Task HasTokenAsync_FalseBeforeIssue_TrueAfterIssue()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        bool beforeIssue = await settings.HasTokenAsync(CancellationToken.None);
        await settings.IssueTokenAsync(CancellationToken.None);
        bool afterIssue = await settings.HasTokenAsync(CancellationToken.None);

        beforeIssue.Should().BeFalse();
        afterIssue.Should().BeTrue();
    }

    [Fact]
    public async Task IssueTokenAsync_ReturnsPlaintext_ThatVerifiesTrue()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        string plaintext = await settings.IssueTokenAsync(CancellationToken.None);
        bool verified = await settings.VerifyTokenAsync(plaintext, CancellationToken.None);

        plaintext.Should().NotBeNullOrEmpty();
        verified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyTokenAsync_WrongToken_ReturnsFalse()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        await settings.IssueTokenAsync(CancellationToken.None);
        bool verified = await settings.VerifyTokenAsync("wrong", CancellationToken.None);

        verified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTokenAsync_EmptyOrNullInput_ReturnsFalse()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        await settings.IssueTokenAsync(CancellationToken.None);
        bool verifiedEmpty = await settings.VerifyTokenAsync(string.Empty, CancellationToken.None);
        bool verifiedNull = await settings.VerifyTokenAsync(null, CancellationToken.None);

        verifiedEmpty.Should().BeFalse();
        verifiedNull.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTokenAsync_BeforeAnyIssue_ReturnsFalse()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        bool verified = await settings.VerifyTokenAsync("anything", CancellationToken.None);

        verified.Should().BeFalse();
    }

    [Fact]
    public async Task IssueTokenAsync_Reissue_InvalidatesPreviousToken()
    {
        IntakeSettings settings = new(new FakeConfigurationStore());

        string firstToken = await settings.IssueTokenAsync(CancellationToken.None);
        string secondToken = await settings.IssueTokenAsync(CancellationToken.None);

        bool firstStillVerifies = await settings.VerifyTokenAsync(
            firstToken,
            CancellationToken.None
        );
        bool secondVerifies = await settings.VerifyTokenAsync(secondToken, CancellationToken.None);

        firstToken.Should().NotBe(secondToken);
        firstStillVerifies.Should().BeFalse();
        secondVerifies.Should().BeTrue();
    }

    private sealed class FakeConfigurationStore : IConfigurationStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string? GetValue(string key) =>
            _values.TryGetValue(key, out string? value) ? value : null;

        public void SetValue(string key, string value) => _values[key] = value;

        public Task SetValueAsync(string key, string value, Guid? modifiedBy = null)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public bool HasKey(string key) => _values.ContainsKey(key);
    }
}
