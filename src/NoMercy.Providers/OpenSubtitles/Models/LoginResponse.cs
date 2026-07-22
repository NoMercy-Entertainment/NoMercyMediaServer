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

using System.Xml.Serialization;

namespace NoMercy.Providers.OpenSubtitles.Models;

[XmlRoot(elementName: "methodResponse", IsNullable = false)]
public class LoginResponse
{
    [XmlElement(elementName: "params", IsNullable = false)]
    public LoginResponseParams? Params { get; set; }
}

public class LoginResponseParams
{
    [XmlElement(elementName: "param", IsNullable = false)]
    public LoginResponseParam? Param { get; set; }
}

public class LoginResponseParam
{
    [XmlElement(elementName: "value", IsNullable = false)]
    public LoginResponseValue? Value { get; set; }
}

public class LoginResponseValue
{
    [XmlElement(elementName: "string", IsNullable = true)]
    public string? String { get; set; }

    [XmlElement(elementName: "double", IsNullable = true)]
    public double? Double { get; set; }

    [XmlElement(elementName: "struct", IsNullable = true)]
    public LoginResponseStruct? Struct { get; set; }
}

public class LoginResponseMember
{
    [XmlElement(elementName: "name", IsNullable = true)]
    public string? Name { get; set; }

    [XmlElement(elementName: "value", IsNullable = true)]
    public LoginResponseValue? Value { get; set; }
}

public class LoginResponseStruct
{
    [XmlElement(elementName: "member", IsNullable = true)]
    public List<LoginResponseMember> Member { get; set; } = [];
}
