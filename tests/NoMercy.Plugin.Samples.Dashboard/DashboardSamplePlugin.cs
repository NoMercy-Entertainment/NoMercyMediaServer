// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2024 NoMercy Entertainment

using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugin.Samples.Dashboard;

/// <summary>
/// The reference for a plugin with a screen.
///
/// It exists to be copied. Everything here is the shape a real plugin should
/// have: a tree of components rather than markup, a view that answers each
/// surface on its own terms, and text referenced by key so it can be translated.
/// </summary>
public class DashboardSamplePlugin : IUiPlugin
{
    public Guid Id => Guid.Parse("66666666-7777-8888-9999-000000000000");

    public string Name => "Dashboard Sample";

    public string Description => "Reference plugin: a screen built from components";

    public Version Version => new(0, 1, 0);

    public IReadOnlyList<PluginNavEntry> NavEntries =>
    [
        new()
        {
            Section = "Library",
            // A key, not a label. The client resolves it under this plugin's
            // namespace, so a Dutch viewer reads Dutch here too.
            Label = "title",
            Route = "/",
            Icon = "grid"
        }
    ];

    public void Initialize(IPluginContext context)
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public Task<PluginView> GetViewAsync(PluginViewRequest request, CancellationToken ct)
    {
        // Branch only where the page is genuinely a different page. A television
        // at four metres is not a narrow desktop. Differences of a single hidden
        // element belong on that element's own box instead.
        return Task.FromResult(request.Surface switch
        {
            PluginSurface.Tv => Screen(columns: 4, showDetails: false),
            PluginSurface.Mobile => Screen(columns: 1, showDetails: false),
            _ => Screen(columns: 3, showDetails: true)
        });
    }

    /// <summary>
    /// One screen. Every string is a key the client resolves, never English
    /// written into the payload.
    /// </summary>
    private static PluginView Screen(int columns, bool showDetails)
    {
        PluginComponent card = new()
        {
            Id = "recent",
            Component = "NMCard",
            Props = new()
            {
                ["box"] = new Dictionary<string, object?>
                {
                    ["padding"] = new Dictionary<string, object?> { ["all"] = "4" },
                    ["columns"] = columns
                }
            },
            Items =
            [
                new()
                {
                    Id = "heading",
                    Component = "NMContentHeader",
                    Props = new() { ["titleText"] = "title" }
                },
                new()
                {
                    Id = "play",
                    Component = "NMButton",
                    Props = new() { ["ariaLabel"] = "play" }
                }
            ]
        };

        if (showDetails)
            card.Items.Add(new()
            {
                Id = "details",
                Component = "NMTable",
                // Hidden rather than absent on the small surfaces, so one tree
                // serves all three where the difference is only what shows.
                Props = new()
                {
                    ["box"] = new Dictionary<string, object?>
                    {
                        ["hiddenOn"] = new[] { PluginSurface.Mobile, PluginSurface.Tv }
                    }
                }
            });

        return new() { Components = [card] };
    }
}
