using TagLib;
using FileTag = TagLib.File;

namespace NoMercy.NmSystem.Dto;

public class TagFile
{
    public static TagFile Create(string path)
    {
        FileTag? fileTag = FileTag.Create(path);
        fileTag.Tag.Pictures = [];
        return new TagFile()
        {
            Tag = fileTag.Tag,
            Properties = fileTag.Properties
        };
    }
    
    public Tag? Tag { get; set; }
    public Properties? Properties { get; set; }
}