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

[XmlRoot("methodResponse")]
public class SubtitleSearchResponse
{
    [XmlArray("params")]
    [XmlArrayItem("param")]
    public List<SubtitleSearchResponseParam> Params { get; set; } = new();
}

public class SubtitleSearchResponseParam
{
    [XmlElement("value")]
    public SubtitleSearchResponseMemberValue Value { get; set; } = new();
}

public class SubtitleSearchResponseResponseValue
{
    [XmlElement("struct")]
    public SubtitleSearchResponseStruct Struct { get; set; } = new();
}

public class SubtitleSearchResponseStruct
{
    [XmlElement("member")]
    public List<SubtitleSearchResponseMember> Members { get; set; } = new();
}

public class SubtitleSearchResponseMember
{
    [XmlElement("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("value")]
    public SubtitleSearchResponseMemberValue MemberValue { get; set; } = new();
}

public class SubtitleSearchResponseMemberValue
{
    [XmlElement("struct", IsNullable = true)]
    public SubtitleSearchResponseStruct InnerStruct { get; set; } = new();

    [XmlElement("array", IsNullable = true)]
    public SubtitleSearchResponseArrayData ArrayData { get; set; } = new();

    [XmlElement("string", IsNullable = true)]
    public string StringValue { get; set; } = string.Empty;

    [XmlElement("double", IsNullable = true)]
    public double? DoubleValue { get; set; }

    [XmlElement("int", IsNullable = true)]
    public int? IntValue { get; set; }
}

public class SubtitleSearchResponseArrayData
{
    [XmlArray("data")]
    [XmlArrayItem("value")]
    public List<SubtitleSearchResponseMemberValue> Values { get; set; } = [];
}
