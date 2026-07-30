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

namespace NoMercy.Plugins.Abstractions;

/// <summary>
/// Builds a <see cref="PluginView"/> without anyone hand-writing the descriptor
/// tree.
/// <para>
/// <see cref="PluginComponent"/> is deliberately generic — a tag and a bag of
/// props — which is what lets a client render any view with one recursive walk.
/// The cost is that nothing stops an author putting the wrong keys in the bag,
/// and a wrong key renders as an empty node rather than an error. These
/// factories are where the right keys are written down once.
/// </para>
/// </summary>
public static class PluginViews
{
    public static PluginView Declarative(params PluginComponent[] components) =>
        new() { Components = [.. components] };

    public static PluginView Declarative(
        int refreshInterval,
        params PluginComponent[] components
    ) => new() { Components = [.. components], RefreshInterval = refreshInterval };

    public static PluginView WebView(string entryUrl) =>
        new() { WebView = new() { EntryUrl = entryUrl } };

    public static PluginComponent Container(string id, params PluginComponent[] children) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Container,
            Items = [.. children],
        };

    public static PluginComponent Text(string id, string value, string? variant = null) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Text,
            Props = new() { ["value"] = value, ["variant"] = variant },
        };

    public static PluginComponent Image(string id, string url, string? alt = null) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Image,
            Props = new() { ["url"] = url, ["alt"] = alt },
        };

    public static PluginComponent List(string id, params PluginComponent[] items) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.List,
            Items = [.. items],
        };

    public static PluginComponent Row(string id, params PluginComponent[] items) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Row,
            Items = [.. items],
        };

    public static PluginComponent Grid(string id, params PluginComponent[] items) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Grid,
            Items = [.. items],
        };

    public static PluginComponent Card(
        string id,
        string title,
        string? subtitle = null,
        string? image = null,
        PluginActionIntent? action = null
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Card,
            Props = new()
            {
                ["title"] = title,
                ["subtitle"] = subtitle,
                ["image"] = image,
            },
            Action = action,
        };

    public static PluginComponent Detail(
        string id,
        string title,
        string? description = null,
        string? image = null,
        params PluginComponent[] children
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Detail,
            Props = new()
            {
                ["title"] = title,
                ["description"] = description,
                ["image"] = image,
            },
            Items = [.. children],
        };

    public static PluginComponent Button(
        string id,
        string label,
        PluginActionIntent action,
        string? icon = null,
        string? variant = null
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Button,
            Props = new()
            {
                ["label"] = label,
                ["icon"] = icon,
                ["variant"] = variant,
            },
            Action = action,
        };

    /// <summary>
    /// A button whose action is confirmed before it runs. Separate from
    /// <see cref="Button"/> so deleting something reads differently at the call
    /// site than pressing play does.
    /// </summary>
    public static PluginComponent DestructiveButton(
        string id,
        string label,
        PluginActionIntent action,
        string confirmTitle,
        string? confirmMessage = null,
        string? confirmLabel = null
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Button,
            Props = new() { ["label"] = label, ["variant"] = "danger" },
            Action = new()
            {
                Type = action.Type,
                Payload = action.Payload,
                Confirm = new()
                {
                    Title = confirmTitle,
                    Message = confirmMessage,
                    ConfirmLabel = confirmLabel,
                    Destructive = true,
                },
            },
        };

    public static PluginComponent Form(
        string id,
        string submitLabel,
        PluginActionIntent submitAction,
        params PluginFormField[] fields
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Form,
            Props = new() { ["submitLabel"] = submitLabel, ["fields"] = fields.ToList() },
            Action = submitAction,
        };

    public static PluginComponent EmptyState(string id, string title, string? message = null) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.EmptyState,
            Props = new() { ["title"] = title, ["message"] = message },
        };

    public static PluginComponent Spinner(string id, string? label = null) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Spinner,
            Props = new() { ["label"] = label },
        };

    /// <summary>
    /// A table. Rows are built with <see cref="Row(string, System.Collections.Generic.IReadOnlyDictionary{string, object?}, PluginActionIntent?)"/>
    /// so a cell lands under its column's key and column order stays
    /// presentation.
    /// </summary>
    public static PluginComponent Table(
        string id,
        IReadOnlyList<PluginTableColumn> columns,
        IReadOnlyList<PluginComponent> rows,
        string? emptyMessage = null
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Table,
            Props = new() { ["columns"] = columns.ToList(), ["emptyMessage"] = emptyMessage },
            Items = [.. rows],
        };

    public static PluginComponent Row(
        string id,
        IReadOnlyDictionary<string, object?> cells,
        PluginActionIntent? action = null
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Row,
            Props = new(cells),
            Action = action,
        };

    /// <summary>
    /// <paramref name="value"/> between 0 and 1 draws a determinate bar; null
    /// draws an indeterminate one.
    /// </summary>
    public static PluginComponent Progress(string id, double? value, string? label = null) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Progress,
            Props = new() { ["value"] = value, ["label"] = label },
        };

    public static PluginComponent Badge(
        string id,
        string label,
        string variant = PluginBadgeVariant.Neutral
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Badge,
            Props = new() { ["label"] = label, ["variant"] = variant },
        };
}
