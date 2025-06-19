using System.Xml.Serialization;

namespace NoMercy.Providers.OpenSubtitles.Models;

[XmlRoot("methodCall")]
public class Login
{
    [XmlElement("methodName")] public string MethodName { get; set; } = null!;

    [XmlArray("params")]
    [XmlArrayItem("param")]
    public LoginParam[] Params { get; set; } = null!;
}

public class LoginParam
{
    [XmlElement("value")] public LoginValue? Value { get; set; }
}

public class LoginValue
{
    [XmlElement("string")] public string? String { get; set; }
}