using System.Xml.Serialization;

namespace NoMercy.Providers.OpenSubtitles.Models;

[XmlRoot("methodResponse", IsNullable = false)]
public class LoginResponse
{
    [XmlElement("params", IsNullable = false)]
    public LoginResponseParams? Params { get; set; }
}

public class LoginResponseParams
{
    [XmlElement("param", IsNullable = false)]
    public LoginResponseParam? Param { get; set; }
}

public class LoginResponseParam
{
    [XmlElement("value", IsNullable = false)]
    public LoginResponseValue? Value { get; set; }
}

public class LoginResponseValue
{
    [XmlElement("string", IsNullable = true)]
    public string? String { get; set; }

    [XmlElement("double", IsNullable = true)]
    public double? Double { get; set; }

    [XmlElement("struct", IsNullable = true)]
    public LoginResponseStruct? Struct { get; set; }
}

public class LoginResponseMember
{
    [XmlElement("name", IsNullable = true)]
    public string? Name { get; set; }

    [XmlElement("value", IsNullable = true)]
    public LoginResponseValue? Value { get; set; }
}

public class LoginResponseStruct
{
    [XmlElement("member", IsNullable = true)]
    public List<LoginResponseMember> Member { get; set; } = [];
}