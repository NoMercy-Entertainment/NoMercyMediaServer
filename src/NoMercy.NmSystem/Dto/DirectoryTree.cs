using Newtonsoft.Json;

namespace NoMercy.NmSystem.Dto;

public class DirectoryTree
{
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
    [JsonProperty("mode")] public int Mode { get; set; }
    [JsonProperty("size")] public int? Size { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("parent")] public string Parent { get; set; } = string.Empty;
    [JsonProperty("full_path")] public string FullPath { get; set; } = string.Empty;

    public DirectoryTree()
    {
    }

    public DirectoryTree(string parent, string path)
    {
        string fullPath = System.IO.Path.Combine(parent, path);

        DirectoryInfo pathInfo = new(fullPath);
        FileInfo fileInfo = new(fullPath);

        string type = pathInfo.Attributes.HasFlag(FileAttributes.Directory) ? "folder" : "file";

        string newPath = string.IsNullOrEmpty(pathInfo.Name)
            ? path
            : pathInfo.Name;

        string parentPath = string.IsNullOrEmpty(parent)
            ? "/"
            : System.IO.Path.Combine(fullPath, @"..\..");

        // double dirSize = Task.Run(() => pathInfo.GetDirectorySize())?.Result ?? 0.0;

        Path = newPath;
        Parent = parentPath;
        FullPath = fullPath.Replace(@"..\", "");
        Mode = (int)pathInfo.Attributes;
        Size = type == "file" ? (int?)fileInfo.Length : null;
        Type = type;
    }
}