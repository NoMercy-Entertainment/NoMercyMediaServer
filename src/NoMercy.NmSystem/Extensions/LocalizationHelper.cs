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

using System.Reflection;
using System.Xml.Linq;
using I18N.DotNet;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.Extensions;

public static class LocalizationHelper
{
    public static ILocalizer GlobalLocalizer { get; set; } = new Localizer();

    // Single-writer lock + in-process dedup so two concurrent requests can't
    // race on the I18N.xml file handle and so we don't repeatedly load+save
    // the document for the same missing key while a previous append is mid-write.
    private static readonly object WriteLock = new();
    private static readonly HashSet<string> SeenMissingKeys = new(comparer: StringComparer.Ordinal);

    public static string Localize(this string key)
    {
        string localized = GlobalLocalizer.Localize(text: key);
        if (key == localized && Config.IsDev && IsCollectableKey(key: key))
        {
            TryAppendMissingLocalization(key: key);
            return key;
        }

        return localized;
    }

    // A missing key is only worth recording as a translatable string when it is a
    // stable, human-authored label. Runtime-interpolated messages — "Segment 78 is
    // not ready yet", a served file path, "Maximum 3 sessions" — bake variable data
    // into the text, so every occurrence is a unique throwaway key that pollutes
    // I18N.xml and can never be usefully translated. Any digit or path separator is
    // the tell: a real UI label carries a placeholder token, never a live value.
    private static bool IsCollectableKey(string key) =>
        !key.Any(predicate: c => char.IsDigit(c: c) || c == '\\' || c == '/');

    private static void TryAppendMissingLocalization(string key)
    {
        // A best-effort dev-only convenience must NEVER break a controller
        // response. Localize() is called from DTO setters; an IOException
        // here was bubbling all the way back to the request pipeline and
        // returning 500 to the dashboard.
        try
        {
            AppendMissingLocalization(key: key);
        }
        catch (Exception ex)
        {
            Logger.App(
                message: $"LocalizationHelper: failed to record missing key '{key}': {ex.Message}",
                level: LogEventLevel.Warning
            );
        }
    }

    private static void AppendMissingLocalization(string key)
    {
        // Cheap pre-check outside the lock — vast majority of calls are
        // repeat hits for the same handful of keys during a session.
        lock (SeenMissingKeys)
        {
            if (!SeenMissingKeys.Add(item: key))
                return;
        }

        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string? projectRoot = Directory
            .GetParent(path: baseDirectory)
            ?.Parent?.Parent?.Parent?.Parent?.FullName;
        if (projectRoot is null)
            return;

        string filePath = Path.Combine(path1: projectRoot, path2: "NoMercy.Api", path3: "Resources", path4: "I18N.xml");

        lock (WriteLock)
        {
            XDocument doc = XDocument.Load(uri: filePath);

            // Re-check inside the lock against the on-disk file — a previous
            // session may have added the key already.
            bool exists =
                doc.Root?.Elements(name: "Entry").Any(predicate: e => e.Element(name: "Key")?.Value == key) == true;
            if (exists)
                return;

            XElement newEntry = new(
                name: "Entry", content: [new XElement(name: "Key", content: key), new XElement(name: "Value", content: [new XAttribute(name: "lang", value: "nl"), key])]
            );

            doc.Root?.Add(content: newEntry);
            doc.Save(fileName: filePath);

            // Reload the localizer to include the new entry
            Localizer reportLocalizer = new();
            reportLocalizer.LoadXML(assembly: Assembly.GetExecutingAssembly(), resourceName: "Resources.I18N.xml", language: "nl");
            GlobalLocalizer = reportLocalizer;
        }
    }
}
