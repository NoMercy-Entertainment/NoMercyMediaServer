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

        FileInfo fileInfo = new(fullPath);

        string type = fileInfo.Attributes.HasFlag(FileAttributes.Directory) ? "folder" : "file";

        string newPath = string.IsNullOrEmpty(fileInfo.Name)
            ? path
            : fileInfo.Name;

        string parentPath = string.IsNullOrEmpty(parent)
            ? "/"
            : System.IO.Path.Combine(fullPath, @"..\..");

        Path = newPath;
        Parent = parentPath;
        FullPath = fullPath.Replace(@"..\", "");
        Mode = (int)fileInfo.Attributes;
        Size = type == "file" ? int.Parse(fileInfo.Length.ToString()) : null;
        Type = type;
    }
}