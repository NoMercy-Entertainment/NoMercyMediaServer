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

using Newtonsoft.Json;

namespace NoMercy.NmSystem.Dto;

public class DirectoryTree
{
    [JsonProperty(propertyName: "path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty(propertyName: "mode")]
    public int Mode { get; set; }

    [JsonProperty(propertyName: "size")]
    public long? Size { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "parent")]
    public string Parent { get; set; } = string.Empty;

    [JsonProperty(propertyName: "full_path")]
    public string FullPath { get; set; } = string.Empty;

    [JsonProperty(propertyName: "subtitle", NullValueHandling = NullValueHandling.Ignore)]
    public string? Subtitle { get; set; }

    [JsonProperty(propertyName: "is_empty", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEmpty { get; set; }

    public DirectoryTree() { }

    public DirectoryTree(string parent, string path)
    {
        string fullPath = System.IO.Path.Combine(path1: parent, path2: path);

        DirectoryInfo pathInfo = new(path: fullPath);
        FileInfo fileInfo = new(fileName: fullPath);

        string type = pathInfo.Attributes.HasFlag(flag: FileAttributes.Directory) ? "folder" : "file";

        string newPath = string.IsNullOrEmpty(value: pathInfo.Name) ? path : pathInfo.Name;

        string parentPath = string.IsNullOrEmpty(value: parent)
            ? "/"
            : System.IO.Path.Combine(path1: fullPath, path2: @"..\..");

        // double dirSize = Task.Run(() => pathInfo.GetDirectorySize())?.Result ?? 0.0;

        Path = newPath;
        Parent = parentPath;
        FullPath = fullPath.Replace(oldValue: @"..\", newValue: "");
        Mode = (int)pathInfo.Attributes;
        Size = type == "file" ? fileInfo.Length : null;
        Type = type;
    }
}
