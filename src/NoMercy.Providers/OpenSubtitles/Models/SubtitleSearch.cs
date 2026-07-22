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

public class SubtitleSearch
{
    [XmlElement(elementName: "methodCall")]
    public MethodCall MethodCall { get; set; } = new();
}

public class MethodCall
{
    [XmlElement(elementName: "methodName")]
    public string MethodName { get; set; } = string.Empty;

    [XmlElement(elementName: "params")]
    public SubtitleSearchParams Params { get; set; } = new();
}

public class SubtitleSearchParams
{
    [XmlElement(elementName: "param")]
    public SubtitleSearchParam[] Param { get; set; } = [];
}

public class SubtitleSearchParam
{
    [XmlElement(elementName: "value")]
    public SubtitleSearchParamValue Value { get; set; } = new();
}

public class SubtitleSearchParamValue
{
    [XmlElement(elementName: "string", IsNullable = true)]
    public string String { get; set; } = string.Empty;

    [XmlElement(elementName: "array", IsNullable = true)]
    public SubtitleSearchArray Array { get; set; } = new();
}

public class SubtitleSearchArray
{
    [XmlElement(elementName: "data")]
    public SubtitleSearchData Data { get; set; } = new();
}

public class SubtitleSearchData
{
    [XmlElement(elementName: "value")]
    public SubtitleSearchDataValue Value { get; set; } = new();
}

public class SubtitleSearchDataValue
{
    [XmlElement(elementName: "struct")]
    public SubtitleSearchStruct Struct { get; set; } = new();
}

public class SubtitleSearchStruct
{
    [XmlElement(elementName: "member")]
    public SubtitleSearchMember[] Member { get; set; } = [];
}

public class SubtitleSearchMember
{
    public SubtitleSearchMember()
    {
        //
    }

    public SubtitleSearchMember(string name, SubtitleSearchMemberValue value)
    {
        Name = name;
        Value = value;
    }

    [XmlElement(elementName: "name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement(elementName: "value")]
    public SubtitleSearchMemberValue Value { get; set; } = new();
}

public class SubtitleSearchMemberValue
{
    [XmlElement(elementName: "string")]
    public string String { get; set; } = string.Empty;
}
