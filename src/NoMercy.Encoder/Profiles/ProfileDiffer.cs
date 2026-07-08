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

namespace NoMercy.Encoder.Profiles;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

public static class ProfileDiffer
{
    private static readonly JsonSerializer CamelSerializer = JsonSerializer.Create(
        new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        }
    );

    public static JObject Diff(EncodingProfile child, EncodingProfile resolvedParent)
    {
        JObject childJson = JObject.FromObject(child, CamelSerializer);
        JObject parentJson = JObject.FromObject(resolvedParent, CamelSerializer);
        return DiffObject(childJson, parentJson);
    }

    private static JObject DiffObject(JObject child, JObject parent)
    {
        JObject result = new();
        foreach (JProperty prop in child.Properties())
        {
            JToken? parentValue = parent[prop.Name];
            JToken childValue = prop.Value;

            if (parentValue is null || !JToken.DeepEquals(childValue, parentValue))
            {
                result[prop.Name] = childValue.DeepClone();
            }
        }
        return result;
    }
}
