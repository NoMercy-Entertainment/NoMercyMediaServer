namespace NoMercy.NmSystem.Configuration;

public class ContentPolicy
{
    public bool? AllowAdultContent { get; set; }

    public bool ShowAdultContent => AllowAdultContent == true;
}
