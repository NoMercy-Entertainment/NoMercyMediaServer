using Newtonsoft.Json;
using TagLib;
using FileTag = TagLib.File;

namespace NoMercy.NmSystem.Dto;

public class TagFile
{
    public static TagFile Create(string path)
    {
        using FileTag? fileTag = FileTag.Create(path);
        fileTag.Tag.Pictures = [];
        return new()
        {
            Tag = fileTag.Tag,
            Properties = fileTag.Properties
        };
    }

    [JsonIgnore] public Tag? Tag { get; set; }
    public Properties? Properties { get; set; }
}