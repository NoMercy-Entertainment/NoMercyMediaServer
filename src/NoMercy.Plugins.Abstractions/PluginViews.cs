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

    /// <summary>
    /// Answer one surface from a set declared per platform.
    ///
    /// The shape a plugin should reach for instead of a switch: the platforms
    /// sit beside each other, an unadapted one is visible rather than implied,
    /// and the fallback is named rather than being whichever branch came last.
    /// </summary>
    public static PluginView PerSurface(
        string surface,
        PluginView fallback,
        PluginView? web = null,
        PluginView? mobile = null,
        PluginView? tv = null
    ) =>
        new PluginSurfaceViews
        {
            Fallback = fallback,
            Web = web,
            Mobile = mobile,
            Tv = tv,
        }.For(surface);

    public static PluginView WebView(string entryUrl) =>
        new() { WebView = new() { EntryUrl = entryUrl } };

    public static PluginComponent Container(string id, params PluginComponent[] children) =>
        Stack(id, "column", children);

    /// <summary>
    /// A ghost card laid out along one axis.
    /// <para>
    /// The design system ships no container, row or grid component, and does
    /// not need one — <c>box</c> carries direction, wrap and gap, and resolves
    /// to flex or grid on its own. A layout tag would be a component that
    /// exists only to hold a style the box already holds.
    /// </para>
    /// </summary>
    private static PluginComponent Stack(
        string id,
        string direction,
        IReadOnlyList<PluginComponent> children,
        bool wrap = false
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Container,
            Props = new()
            {
                ["variant"] = "ghost",
                ["box"] = new Dictionary<string, object?>
                {
                    ["direction"] = direction,

                    ["width"] = "full",
                    ["wrap"] = wrap ? "wrap" : null,
                    ["gap"] = new Dictionary<string, object?> { ["all"] = "3" },
                },
            },
            Items = [.. children],
        };

    public static PluginComponent Text(string id, string value, string? variant = null) =>
        variant switch
        {
            "title" or "subtitle" => new()
            {
                Id = id,
                Component = "NMContentHeader",
                Items = [Leaf($"{id}-text", value)],
            },
            "caption" or "muted" => new()
            {
                Id = id,
                Component = "NMHelper",
                Props = new() { ["helperText"] = value },
            },
            _ => Leaf(id, value),
        };

    /// <summary>
    /// The one leaf the design system does not draw. Every component takes its
    /// content from a slot and a payload can only put components in a slot, so
    /// without this a server-built tree had no way to say a word.
    /// </summary>
    private static PluginComponent Leaf(string id, string value) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Text,
            Props = new() { ["text"] = value },
        };

    public static PluginComponent Image(string id, string url, string? alt = null) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Image,
            // A frame that holds its shape before the image arrives and keeps it
            // when the image never does. Artwork urls rot, and a frame with no
            // shape reserved collapses to a sliver.
            Props = new()
            {
                ["src"] = url,
                ["alt"] = alt,
                ["fit"] = "contain",
                ["aspectRatio"] = "square",
            },
        };

    public static PluginComponent List(string id, params PluginComponent[] items) =>
        Stack(id, "column", items);

    public static PluginComponent Row(string id, params PluginComponent[] items) =>
        Stack(id, "row", items, wrap: true);

    /// <summary>
    /// Tiles that wrap. A wrapping row rather than a fixed column count,
    /// because the payload cannot know how wide the viewer's screen is and a
    /// number chosen here would be wrong on most of them.
    /// </summary>
    public static PluginComponent Grid(string id, params PluginComponent[] items) =>
        Stack(id, "row", items, wrap: true);

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
                ["box"] = new Dictionary<string, object?>
                {
                    ["direction"] = "column",

                    ["width"] = "full",
                    ["gap"] = new Dictionary<string, object?> { ["all"] = "2" },
                    ["padding"] = new Dictionary<string, object?> { ["all"] = "3" },
                },
            },
            Items = [.. Face($"{id}", title, subtitle, image)],
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
                ["box"] = new Dictionary<string, object?>
                {
                    ["direction"] = "column",

                    ["width"] = "full",
                    ["gap"] = new Dictionary<string, object?> { ["all"] = "3" },
                    ["padding"] = new Dictionary<string, object?> { ["all"] = "4" },
                },
            },
            Items = [.. Face(id, title, description, image), .. children],
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
            // A button reads its label from what is inside it. Setting only a
            // label prop drew an empty square that a reader announced and a
            // person could not.
            Props = new()
            {
                ["ariaLabel"] = label,
                ["icon"] = icon,
                ["variant"] = variant ?? "secondary",
                ["box"] = new Dictionary<string, object?>
                {
                    // NMButton fills its container by default, so in a column it
                    // drew a banner the width of the page rather than something
                    // to press.
                    ["alignSelf"] = "start",
                    ["width"] = "content",
                },
            },
            Items = [Leaf($"{id}-label", label)],
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
            Props = new()
            {
                ["ariaLabel"] = label,
                ["variant"] = "destructive",
                ["box"] = new Dictionary<string, object?>
                {
                    // NMButton fills its container by default, so in a column it
                    // drew a banner the width of the page rather than something
                    // to press.
                    ["alignSelf"] = "start",
                    ["width"] = "content",
                },
            },
            Items = [Leaf($"{id}-label", label)],
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
            Props = new()
            {
                ["box"] = new Dictionary<string, object?>
                {
                    ["direction"] = "column",

                    ["width"] = "full",
                    ["gap"] = new Dictionary<string, object?> { ["all"] = "3" },
                    ["padding"] = new Dictionary<string, object?> { ["all"] = "4" },
                },
            },
            // A field is a component, not an entry in a bag. Declaring them as
            // props left the card with nothing inside it and drew an empty bar
            // where the form should be.
            Items =
            [
                .. fields.Select(field => Field(id, field)),
                Button($"{id}-submit", submitLabel, submitAction, variant: "primary"),
            ],
            Action = submitAction,
        };

    public static PluginComponent EmptyState(string id, string title, string? message = null) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.EmptyState,
            // Two text leaves side by side share a line, so the empty state read
            // "No indexer configuredAdd an indexer so…" as one sentence. The
            // heading and the line under it are different things and have to be
            // different components to sit apart.
            Items = [.. Face(id, title, message, null)],
        };

    public static PluginComponent Spinner(string id, string? label = null) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Spinner,
            Props = new() { ["ariaLabel"] = label },
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
            Props = new()
            {
                ["variant"] = "ghost",
                ["accessibility"] = new Dictionary<string, object?> { ["role"] = "table" },
                ["box"] = new Dictionary<string, object?>
                {
                    ["direction"] = "column",
                    ["width"] = "full",
                    ["gap"] = new Dictionary<string, object?> { ["all"] = "1" },
                },
            },
            Items =
            [
                HeaderRow(id, columns),
                .. rows.Select(row => BodyRow(columns, row)),
                .. rows.Count == 0 && emptyMessage is not null
                    ? new[] { Text($"{id}-empty", emptyMessage, "caption") }
                    : [],
            ],
        };

    /// <summary>
    /// A row of column labels, announced as one.
    /// <para>
    /// Not an <c>NMTable</c>: that is a bare table element with a single slot,
    /// and the design system generates no row or cell to put in it — anything
    /// else placed there is hoisted straight back out by the parser, which is
    /// why this screen drew a heading over an empty space. Rows and cells built
    /// from boxes and given their ARIA roles are a table a reader announces as
    /// one, which is the part that matters.
    /// </para>
    /// </summary>
    private static PluginComponent HeaderRow(string id, IReadOnlyList<PluginTableColumn> columns) =>
        TableRow(
            $"{id}-head",
            "row",
            [
                .. columns.Select(column =>
                    Cell(
                        $"{id}-head-{column.Key}",
                        "columnheader",
                        column.Align,
                        Leaf($"{id}-head-{column.Key}-text", column.Label)
                    )
                ),
            ]
        );

    /// <summary>One row's cells, in column order, each holding whatever that column asked for.</summary>
    private static PluginComponent BodyRow(
        IReadOnlyList<PluginTableColumn> columns,
        PluginComponent row
    ) =>
        TableRow(
            row.Id,
            "row",
            [
                .. columns.Select(column =>
                    Cell(
                        $"{row.Id}-{column.Key}",
                        "cell",
                        column.Align,
                        CellContent(
                            $"{row.Id}-{column.Key}",
                            column,
                            row.Props.GetValueOrDefault(column.Key)
                        )
                    )
                ),
            ],
            row.Action
        );

    private static PluginComponent TableRow(
        string id,
        string role,
        IReadOnlyList<PluginComponent> cells,
        PluginActionIntent? action = null
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Container,
            Props = new()
            {
                ["variant"] = "ghost",
                ["accessibility"] = new Dictionary<string, object?> { ["role"] = role },
                ["box"] = new Dictionary<string, object?>
                {
                    ["direction"] = "row",
                    ["width"] = "full",
                    ["align"] = "center",
                    ["gap"] = new Dictionary<string, object?> { ["all"] = "3" },
                },
            },
            Items = [.. cells],
            Action = action,
        };

    private static PluginComponent Cell(
        string id,
        string role,
        string? align,
        PluginComponent content
    ) =>
        new()
        {
            Id = id,
            Component = PluginComponentType.Container,
            Props = new()
            {
                ["variant"] = "ghost",
                ["accessibility"] = new Dictionary<string, object?> { ["role"] = role },
                ["box"] = new Dictionary<string, object?>
                {
                    ["direction"] = "row",
                    ["grow"] = 1,
                    ["justify"] = align == "right" ? "end" : "start",
                },
            },
            Items = [content],
        };

    /// <summary>
    /// What a cell draws, from what its column said it holds. A progress column
    /// carrying a number is a bar; a badge column is a badge; everything else is
    /// the value as text, because a cell with nothing in it reads as missing
    /// data rather than an empty string.
    /// </summary>
    private static PluginComponent CellContent(string id, PluginTableColumn column, object? value)
    {
        if (column.Cell == PluginTableCellType.Progress && value is double fraction)
            return Progress(id, fraction);

        if (column.Cell == PluginTableCellType.Badge && value is not null)
            return Badge(id, value.ToString() ?? string.Empty);

        return Leaf(id, value?.ToString() ?? string.Empty);
    }

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
            // NMProgress reads 0-100; the contract has always been a fraction.
            Props = new()
            {
                ["value"] = value is null ? null : value * 100,
                ["ariaLabel"] = label,
                ["labelPos"] = label is null ? "none" : "label-right",
            },
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
            // NMBadge's variant is its shape, not its meaning. The meaning
            // travels on the surface, where the design system keeps semantic
            // colour, so a payload still never names one.
            Props = new()
            {
                ["text"] = label,
                ["variant"] = "solid",
                ["surface"] = new Dictionary<string, object?> { ["status"] = variant },
            },
        };

    /// <summary>Artwork, a heading, and the line under it — whichever exist.</summary>
    private static IEnumerable<PluginComponent> Face(
        string id,
        string title,
        string? secondary,
        string? image
    )
    {
        if (!string.IsNullOrWhiteSpace(image))
            yield return Image($"{id}-art", image, title);

        yield return new()
        {
            Id = $"{id}-heading",
            Component = "NMContentHeader",
            Items = [Leaf($"{id}-title", title)],
        };

        // A separate line rather than a second child of the header: the header
        // lays its children out in one row, so both read as one run-on sentence
        // when they share it.
        if (!string.IsNullOrWhiteSpace(secondary))
            yield return new()
            {
                Id = $"{id}-secondary",
                Component = "NMHelper",
                Props = new() { ["helperText"] = secondary },
            };
    }

    /// <summary>
    /// One field, as the component that collects it. A label is a sibling
    /// rather than a prop on the control: only the checkbox declares one, and
    /// the rest would have dropped it silently.
    /// </summary>
    private static PluginComponent Field(string formId, PluginFormField field)
    {
        string id = $"{formId}-{field.Name}";

        if (field.Type == PluginFormFieldType.Checkbox || field.Type == PluginFormFieldType.Toggle)
            return new()
            {
                Id = id,
                Component = field.Type == PluginFormFieldType.Toggle ? "NMToggle" : "NMCheckbox",
                Props = new()
                {
                    ["name"] = field.Name,
                    ["labelText"] = field.Label,
                    ["checked"] = field.Value as bool? ?? false,
                    ["ariaLabel"] = field.Label,
                },
            };

        string component = field.Type switch
        {
            PluginFormFieldType.Select => "NMSelect",
            PluginFormFieldType.File => "NMFileUpload",
            _ => "NMInput",
        };

        Dictionary<string, object?> props = new()
        {
            ["name"] = field.Name,
            ["placeholder"] = field.Placeholder ?? field.Label,
            ["required"] = field.Required,
            ["value"] = field.Value,
        };

        if (field.Type == PluginFormFieldType.Password || field.Type == PluginFormFieldType.Number)
            props["type"] = field.Type;

        if (field.Accept is not null)
            props["accept"] = field.Accept;

        if (field.Multiple)
            props["multiple"] = true;

        if (field.Options.Count > 0)
            props["options"] = field.Options;

        return new()
        {
            Id = $"{id}-group",
            Component = PluginComponentType.Container,
            Props = new()
            {
                ["variant"] = "ghost",
                ["box"] = new Dictionary<string, object?>
                {
                    ["direction"] = "column",

                    ["width"] = "full",
                    ["gap"] = new Dictionary<string, object?> { ["all"] = "1" },
                },
            },
            Items =
            [
                new()
                {
                    Id = $"{id}-label",
                    Component = "NMFormLabel",
                    Items = [Leaf($"{id}-label-text", field.Label)],
                },
                new()
                {
                    Id = id,
                    Component = component,
                    Props = props,
                },
            ],
        };
    }
}
