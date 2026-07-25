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
    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("mode")]
    public int Mode { get; set; }

    [JsonProperty("size")]
    public long? Size { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("parent")]
    public string Parent { get; set; } = string.Empty;

    [JsonProperty("full_path")]
    public string FullPath { get; set; } = string.Empty;

    [JsonProperty("subtitle", NullValueHandling = NullValueHandling.Ignore)]
    public string? Subtitle { get; set; }

    [JsonProperty("is_empty", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEmpty { get; set; }

    public DirectoryTree() { }

    public DirectoryTree(string parent, string path)
    {
        string fullPath = System.IO.Path.Combine(parent, path);

        DirectoryInfo pathInfo = new(fullPath);
        FileInfo fileInfo = new(fullPath);

        string type = pathInfo.Attributes.HasFlag(FileAttributes.Directory) ? "folder" : "file";

        string newPath = string.IsNullOrEmpty(pathInfo.Name) ? path : pathInfo.Name;

        string parentPath = string.IsNullOrEmpty(parent)
            ? "/"
            : System.IO.Path.Combine(fullPath, @"..\..");

        // double dirSize = Task.Run(() => pathInfo.GetDirectorySize())?.Result ?? 0.0;

        Path = newPath;
        Parent = parentPath;
        FullPath = fullPath.Replace(@"..\", "");
        Mode = (int)pathInfo.Attributes;
        Size = type == "file" ? fileInfo.Length : null;
        Type = type;
    }
}
