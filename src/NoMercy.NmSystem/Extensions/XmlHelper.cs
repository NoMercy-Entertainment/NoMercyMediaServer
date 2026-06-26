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

using System.Text;
using System.Xml.Serialization;

namespace NoMercy.NmSystem.Extensions;

public static class XmlHelper
{
    public static string ToXml<T>(this T obj)
    {
        XmlSerializer serializer = new(typeof(T));
        using MemoryStream memoryStream = new();
        using StreamWriter streamWriter = new(memoryStream, Encoding.UTF8);
        serializer.Serialize(streamWriter, obj);
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    public static T? FromXml<T>(this string xml)
    {
        if (string.IsNullOrEmpty(xml))
            return default;

        XmlSerializer serializer = new(typeof(T));
        using StringReader stringReader = new(xml);
        return (T?)serializer.Deserialize(stringReader);
    }
}
