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

[XmlRoot(elementName: "methodCall")]
public class Login
{
    [XmlElement(elementName: "methodName")]
    public string MethodName { get; set; } = null!;

    [XmlArray(elementName: "params")]
    [XmlArrayItem(elementName: "param")]
    public LoginParam[] Params { get; set; } = null!;
}

public class LoginParam
{
    [XmlElement(elementName: "value")]
    public LoginValue? Value { get; set; }
}

public class LoginValue
{
    [XmlElement(elementName: "string")]
    public string? String { get; set; }
}
