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

using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// The declarative UI contract is rendered by three clients that each
/// deserialise it independently, and every drift between them is silent: an
/// unknown component string resolves to nothing in the web map, and a missing
/// branch fails to deserialise in Kotlin. Neither surfaces as an error, so the
/// wire shape is pinned here.
/// </summary>
[Trait("Category", "Unit")]
public class PluginViewContractTests
{
    /// <summary>
    /// The settings MVC builds for the pipeline this server registers, so a
    /// claim about the wire is a claim about the wire. This used
    /// <c>System.Text.Json</c>, which writes no response here: it honoured
    /// attributes Newtonsoft cannot see, so these assertions passed while every
    /// payload went out carrying a second copy of each component's children
    /// beside its props, and every card in the app drew an empty body.
    /// </summary>
    private static readonly JsonSerializerSettings ApiSettings =
        new MvcNewtonsoftJsonOptions().SerializerSettings;

    private static string Serialize(object value) =>
        JsonConvert.SerializeObject(value, ApiSettings);

    [Fact]
    public void PlayMedia_CarriesAStableTypeAndPayload()
    {
        PluginActionIntent action = PluginActionIntent.PlayMedia(
            streamUrl: "https://ice1.somafm.com/groovesalad",
            title: "Groove Salad"
        );

        action.Type.Should().Be("playMedia");
        action.Payload["streamUrl"].Should().Be("https://ice1.somafm.com/groovesalad");
        action.Payload["title"].Should().Be("Groove Salad");
    }

    [Fact]
    public void AView_SerializesItsTagsExactlyAsAClientReadsThem()
    {
        PluginView view = PluginViews.Declarative(
            PluginViews.Grid(
                "stations",
                PluginViews.Card(
                    "s1",
                    "Groove Salad",
                    action: PluginActionIntent.PlayMedia("https://x/gs", "Groove Salad")
                )
            )
        );

        string json = Serialize(view);

        json.Should().Contain("\"component\":\"PluginGrid\"");
        json.Should().Contain("\"component\":\"PluginCard\"");
        json.Should().Contain("\"playMedia\"");
        view.WebView.Should().BeNull();
    }

    [Fact]
    public void AWebViewOnly_ViewHasNoComponentTree()
    {
        PluginView view = PluginViews.WebView("https://plugin.local/index.html");

        view.Components.Should().BeNull();
        view.WebView!.EntryUrl.Should().Be("https://plugin.local/index.html");
    }

    [Fact]
    public void EveryFactoryEmitsATagTheClientsAreToldAbout()
    {
        // A factory emitting a tag missing from the vocabulary is exactly the
        // silent hole this contract exists to prevent: it renders nothing on
        // web and refuses to deserialise on Compose, and no test would notice.
        List<PluginComponent> everyKind =
        [
            PluginViews.Container("a"),
            PluginViews.Text("b", "t"),
            PluginViews.Image("c", "u"),
            PluginViews.List("d"),
            PluginViews.Row("e"),
            PluginViews.Grid("f"),
            PluginViews.Card("g", "t"),
            PluginViews.Detail("h", "t"),
            PluginViews.Button("i", "l", PluginActionIntent.RefreshView()),
            PluginViews.DestructiveButton("j", "l", PluginActionIntent.RefreshView(), "Sure?"),
            PluginViews.Form("k", "Go", PluginActionIntent.RefreshView()),
            PluginViews.EmptyState("l", "t"),
            PluginViews.Spinner("m"),
            PluginViews.Table("n", [], []),
            PluginViews.Progress("o", 0.5),
            PluginViews.Badge("p", "l"),
        ];

        everyKind
            .Select(component => component.Component)
            .Should()
            .OnlyContain(tag => PluginComponentType.All.Contains(tag));
    }

    [Fact]
    public void EveryActionFactoryEmitsAKnownType()
    {
        List<PluginActionIntent> everyIntent =
        [
            PluginActionIntent.PlayMedia("u", "t"),
            PluginActionIntent.Enqueue("u", "t"),
            PluginActionIntent.Navigate("/x"),
            PluginActionIntent.CallPlugin("m"),
            PluginActionIntent.OpenWebView("u"),
            PluginActionIntent.RefreshView(),
        ];

        everyIntent
            .Select(intent => intent.Type)
            .Should()
            .OnlyContain(type => PluginActionType.All.Contains(type));
    }

    [Fact]
    public void ATable_CarriesItsColumnsAndItsRowsCellsByKey()
    {
        // The torrent case: name, progress, state, and a per-row action.
        PluginComponent table = PluginViews.Table(
            "downloads",
            [
                new() { Key = "name", Label = "Name" },
                new()
                {
                    Key = "progress",
                    Label = "Progress",
                    Cell = PluginTableCellType.Progress,
                },
                new()
                {
                    Key = "state",
                    Label = "State",
                    Cell = PluginTableCellType.Badge,
                },
            ],
            [
                PluginViews.Row(
                    "t1",
                    new Dictionary<string, object?>
                    {
                        ["name"] = "ubuntu.iso",
                        ["progress"] = 0.42,
                        ["state"] = "downloading",
                    },
                    PluginActionIntent.CallPlugin("open", new { hash = "abc" })
                ),
            ]
        );

        table.Component.Should().Be(PluginComponentType.Table);
        table.Props["columns"].Should().BeOfType<List<PluginTableColumn>>();
        table.Items.Should().ContainSingle();
        table.Items[0].Props["progress"].Should().Be(0.42);
        table.Items[0].Action!.Type.Should().Be(PluginActionType.CallPlugin);
    }

    [Fact]
    public void ADestructiveButton_ShipsItsConfirmationInTheContract()
    {
        // If the prompt is not on the wire, every client reimplements it and
        // one of them ships a delete button with no prompt at all.
        PluginComponent button = PluginViews.DestructiveButton(
            "remove",
            "Remove",
            PluginActionIntent.CallPlugin("remove", new { hash = "abc" }),
            confirmTitle: "Remove this download?",
            confirmMessage: "The partial files are deleted too."
        );

        button.Action!.Confirm!.Title.Should().Be("Remove this download?");
        button.Action.Confirm.Destructive.Should().BeTrue();
        Serialize(button).Should().Contain("\"confirm\"");
    }

    [Fact]
    public void AnOrdinaryButton_HasNoConfirmKeyOnTheWire()
    {
        PluginComponent button = PluginViews.Button(
            "play",
            "Play",
            PluginActionIntent.PlayMedia("u", "t")
        );

        Serialize(button).Should().NotContain("confirm");
    }

    [Fact]
    public void AFormCanTakeAFileUpload()
    {
        PluginComponent form = PluginViews.Form(
            "add",
            "Add",
            PluginActionIntent.CallPlugin("add"),
            new PluginFormField
            {
                Name = "torrent",
                Label = "Torrent file",
                Type = PluginFormFieldType.File,
                Accept = ".torrent",
                Required = true,
            },
            new PluginFormField
            {
                Name = "startPaused",
                Label = "Start paused",
                Type = PluginFormFieldType.Checkbox,
            }
        );

        string json = Serialize(form);

        json.Should().Contain("\"type\":\"file\"");
        json.Should().Contain("\"accept\":\".torrent\"");
        json.Should().Contain("\"type\":\"checkbox\"");
    }

    [Fact]
    public void ABadgeSaysWhatItMeans_NotWhatColourItIs()
    {
        PluginComponent badge = PluginViews.Badge("state", "Seeding", PluginBadgeVariant.Success);

        badge.Props["variant"].Should().Be("success");
        PluginBadgeVariant.All.Should().Contain(badge.Props["variant"]!.ToString());
    }

    [Fact]
    public void AnIndeterminateProgressHasNoValue()
    {
        PluginViews.Progress("p", null).Props["value"].Should().BeNull();
        PluginViews.Progress("p", 0.25).Props["value"].Should().Be(0.25);
    }

    [Fact]
    public void ANavEntryInAnUnknownSectionLandsSomewhereReal()
    {
        // Rejecting it would turn adding a section on one client into a
        // validation failure for every plugin using it on the other.
        PluginUiSection
            .OrFallback("a-section-no-client-knows")
            .Should()
            .Be(PluginUiSection.Tools);
        PluginUiSection.OrFallback(PluginUiSection.Music).Should().Be(PluginUiSection.Music);
    }
}
