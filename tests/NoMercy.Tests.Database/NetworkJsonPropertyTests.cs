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

using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Tests.Database;

[Trait(name: "Category", value: "Characterization")]
public class NetworkJsonPropertyTests
{
    private static string? GetJsonPropertyName(string propertyName)
    {
        PropertyInfo? prop = typeof(Network).GetProperty(name: propertyName);
        Assert.NotNull(@object: prop);
        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(@object: attr);
        return attr.PropertyName;
    }

    [Fact]
    public void NetworkTv_JsonProperty_IsNetworkTv()
    {
        string? name = GetJsonPropertyName(propertyName: "NetworkTv");
        Assert.Equal(expected: "network_tv", actual: name);
    }

    [Fact]
    public void NetworkTv_JsonProperty_IsNotId()
    {
        string? name = GetJsonPropertyName(propertyName: "NetworkTv");
        Assert.NotEqual(expected: "id", actual: name);
    }

    [Fact]
    public void Id_JsonProperty_IsId()
    {
        string? name = GetJsonPropertyName(propertyName: "Id");
        Assert.Equal(expected: "id", actual: name);
    }

    [Fact]
    public void Id_And_NetworkTv_HaveDifferentJsonPropertyNames()
    {
        string? idName = GetJsonPropertyName(propertyName: "Id");
        string? networkTvName = GetJsonPropertyName(propertyName: "NetworkTv");
        Assert.NotEqual(expected: idName, actual: networkTvName);
    }

    [Fact]
    public void Name_JsonProperty_IsName()
    {
        string? name = GetJsonPropertyName(propertyName: "Name");
        Assert.Equal(expected: "name", actual: name);
    }

    [Fact]
    public void Logo_JsonProperty_IsLogo()
    {
        string? name = GetJsonPropertyName(propertyName: "Logo");
        Assert.Equal(expected: "logo", actual: name);
    }

    [Fact]
    public void OriginCountry_JsonProperty_IsOriginCountry()
    {
        string? name = GetJsonPropertyName(propertyName: "OriginCountry");
        Assert.Equal(expected: "origin_country", actual: name);
    }

    [Fact]
    public void Description_JsonProperty_IsDescription()
    {
        string? name = GetJsonPropertyName(propertyName: "Description");
        Assert.Equal(expected: "description", actual: name);
    }

    [Fact]
    public void Headquarters_JsonProperty_IsHeadquarters()
    {
        string? name = GetJsonPropertyName(propertyName: "Headquarters");
        Assert.Equal(expected: "headquarters", actual: name);
    }

    [Fact]
    public void Homepage_JsonProperty_IsHomepage()
    {
        string? name = GetJsonPropertyName(propertyName: "Homepage");
        Assert.Equal(expected: "homepage", actual: name);
    }

    [Fact]
    public void Serialization_NetworkTv_UsesNetworkTvKey()
    {
        Network network = new() { Id = 1, Name = "HBO" };

        string json = JsonConvert.SerializeObject(value: network);
        Assert.Contains(expectedSubstring: "\"network_tv\"", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "\"network_tv\":1", actualString: json);
    }

    [Fact]
    public void Serialization_Id_UsesIdKey()
    {
        Network network = new() { Id = 42, Name = "Netflix" };

        string json = JsonConvert.SerializeObject(value: network);
        Assert.Contains(expectedSubstring: "\"id\":42", actualString: json);
    }

    [Fact]
    public void Serialization_NoDuplicateIdKeys()
    {
        Network network = new() { Id = 1, Name = "Test" };

        string json = JsonConvert.SerializeObject(value: network);

        int idCount = 0;
        int index = 0;
        while ((index = json.IndexOf(value: "\"id\"", startIndex: index, comparisonType: StringComparison.Ordinal)) != -1)
        {
            idCount++;
            index += 4;
        }

        Assert.Equal(expected: 1, actual: idCount);
    }

    [Fact]
    public void Deserialization_Id_PopulatesCorrectly()
    {
        string json = """{"id":99,"name":"Test","network_tv":[]}""";
        Network? network = JsonConvert.DeserializeObject<Network>(value: json);

        Assert.NotNull(@object: network);
        Assert.Equal(expected: 99, actual: network.Id);
    }

    [Fact]
    public void Deserialization_RoundTrip_PreservesId()
    {
        Network original = new() { Id = 55, Name = "ABC" };

        string json = JsonConvert.SerializeObject(value: original);
        Network? deserialized = JsonConvert.DeserializeObject<Network>(value: json);

        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: 55, actual: deserialized.Id);
    }

    [Theory]
    [InlineData(data: ["Id", "id"])]
    [InlineData(data: ["Name", "name"])]
    [InlineData(data: ["Logo", "logo"])]
    [InlineData(data: ["OriginCountry", "origin_country"])]
    [InlineData(data: ["Description", "description"])]
    [InlineData(data: ["Headquarters", "headquarters"])]
    [InlineData(data: ["Homepage", "homepage"])]
    [InlineData(data: ["NetworkTv", "network_tv"])]
    public void AllProperties_HaveCorrectJsonPropertyNames(
        string propertyName,
        string expectedJsonName
    )
    {
        string? name = GetJsonPropertyName(propertyName: propertyName);
        Assert.Equal(expected: expectedJsonName, actual: name);
    }

    [Fact]
    public void NoDuplicateJsonPropertyNames()
    {
        PropertyInfo[] properties = typeof(Network).GetProperties(
            bindingAttr: BindingFlags.Public | BindingFlags.Instance
        );
        List<string> jsonNames = [];

        foreach (PropertyInfo prop in properties)
        {
            JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
            if (attr?.PropertyName is not null)
            {
                jsonNames.Add(item: attr.PropertyName);
            }
        }

        int distinctCount = jsonNames.Distinct().Count();
        Assert.Equal(expected: jsonNames.Count, actual: distinctCount);
    }
}
