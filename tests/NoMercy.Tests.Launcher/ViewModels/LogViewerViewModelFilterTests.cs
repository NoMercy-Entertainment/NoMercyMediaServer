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

using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;
using NoMercy.Launcher.ViewModels;
using Xunit;

namespace NoMercy.Tests.Launcher.ViewModels;

/// <summary>
/// REQUIREMENT: the log viewer's level dropdown and search box must combine
/// (AND, not OR) to narrow <see cref="LogViewerViewModel.LogEntries"/> down to
/// <see cref="LogViewerViewModel.FilteredEntries"/>, matching against both the
/// message AND the entry type, case-insensitively. This drives directly off
/// the public property setters (SearchText/SelectedLevel), the same surface
/// the view's TextBox/ComboBox bindings use — no internal-method reach-around.
/// </summary>
public sealed class LogViewerViewModelFilterTests
{
    private static LogViewerViewModel CreateViewModel() => new(new ServerConnection());

    private static LogEntryResponse Entry(string type, string message, string level) =>
        new()
        {
            Type = type,
            Message = message,
            Level = level,
            Color = "#FFFFFF",
            ThreadId = 1,
            Time = DateTime.UtcNow,
        };

    [Fact]
    public void NoFilters_AllEntriesPassThrough()
    {
        LogViewerViewModel viewModel = CreateViewModel();
        viewModel.LogEntries.Add(Entry("Server", "listening on 7626", "Information"));
        viewModel.LogEntries.Add(Entry("Encoder", "job finished", "Debug"));

        viewModel.ClearFilters();

        viewModel.FilteredEntries.Should().HaveCount(2);
    }

    [Fact]
    public void SelectedLevel_FiltersToMatchingLevelOnly()
    {
        LogViewerViewModel viewModel = CreateViewModel();
        viewModel.LogEntries.Add(Entry("Server", "listening", "Information"));
        viewModel.LogEntries.Add(Entry("Encoder", "boom", "Error"));

        viewModel.SelectedLevel = "Error";

        viewModel.FilteredEntries.Should().ContainSingle();
        viewModel.FilteredEntries[0].Message.Should().Be("boom");
    }

    [Fact]
    public void SelectedLevel_IsCaseInsensitive()
    {
        LogViewerViewModel viewModel = CreateViewModel();
        viewModel.LogEntries.Add(Entry("Server", "listening", "information"));

        viewModel.SelectedLevel = "Information";

        viewModel.FilteredEntries.Should().ContainSingle();
    }

    [Fact]
    public void SearchText_MatchesMessageCaseInsensitively()
    {
        LogViewerViewModel viewModel = CreateViewModel();
        viewModel.LogEntries.Add(Entry("Server", "Listening on port 7626", "Information"));
        viewModel.LogEntries.Add(Entry("Server", "unrelated line", "Information"));

        viewModel.SearchText = "listening";

        viewModel.FilteredEntries.Should().ContainSingle();
    }

    [Fact]
    public void SearchText_MatchesEntryTypeWhenMessageDoesNotMatch()
    {
        LogViewerViewModel viewModel = CreateViewModel();
        viewModel.LogEntries.Add(Entry("Encoder", "unrelated message", "Information"));
        viewModel.LogEntries.Add(Entry("Server", "different message", "Information"));

        viewModel.SearchText = "encoder";

        viewModel.FilteredEntries.Should().ContainSingle();
        viewModel.FilteredEntries[0].Type.Should().Be("Encoder");
    }

    [Fact]
    public void SearchTextAndLevel_CombineAsAnd()
    {
        LogViewerViewModel viewModel = CreateViewModel();
        viewModel.LogEntries.Add(Entry("Server", "listening now", "Information"));
        viewModel.LogEntries.Add(Entry("Server", "listening now", "Error"));
        viewModel.LogEntries.Add(Entry("Encoder", "encoding now", "Information"));

        viewModel.SelectedLevel = "Information";
        viewModel.SearchText = "listening";

        viewModel.FilteredEntries.Should().ContainSingle();
        viewModel.FilteredEntries[0].Type.Should().Be("Server");
    }

    [Fact]
    public void ClearFilters_ResetsSearchTextAndLevelToAll()
    {
        LogViewerViewModel viewModel = CreateViewModel();
        viewModel.LogEntries.Add(Entry("Server", "listening", "Error"));
        viewModel.SelectedLevel = "Error";
        viewModel.SearchText = "listening";

        viewModel.ClearFilters();

        viewModel.SearchText.Should().Be(string.Empty);
        viewModel.SelectedLevel.Should().Be("All");
        viewModel.FilteredEntries.Should().ContainSingle();
    }

    [Fact]
    public void WhitespaceOnlySearchText_TreatedAsNoFilter()
    {
        LogViewerViewModel viewModel = CreateViewModel();
        viewModel.LogEntries.Add(Entry("Server", "anything", "Information"));

        viewModel.SearchText = "   ";

        viewModel.FilteredEntries.Should().ContainSingle();
    }
}
