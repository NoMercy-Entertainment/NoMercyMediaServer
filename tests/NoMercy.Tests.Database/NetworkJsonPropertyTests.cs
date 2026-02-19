using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Tests.Database;

[Trait("Category", "Characterization")]
public class NetworkJsonPropertyTests
{
    private static string? GetJsonPropertyName(string propertyName)
    {
        PropertyInfo? prop = typeof(Network).GetProperty(propertyName);
        Assert.NotNull(prop);
        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(attr);
        return attr.PropertyName;
    }

    [Fact]
    public void NetworkTv_JsonProperty_IsNetworkTv()
    {
        string? name = GetJsonPropertyName("NetworkTv");
        Assert.Equal("network_tv", name);
    }

    [Fact]
    public void NetworkTv_JsonProperty_IsNotId()
    {
        string? name = GetJsonPropertyName("NetworkTv");
        Assert.NotEqual("id", name);
    }

    [Fact]
    public void Id_JsonProperty_IsId()
    {
        string? name = GetJsonPropertyName("Id");
        Assert.Equal("id", name);
    }

    [Fact]
    public void Id_And_NetworkTv_HaveDifferentJsonPropertyNames()
    {
        string? idName = GetJsonPropertyName("Id");
        string? networkTvName = GetJsonPropertyName("NetworkTv");
        Assert.NotEqual(idName, networkTvName);
    }

    [Fact]
    public void Name_JsonProperty_IsName()
    {
        string? name = GetJsonPropertyName("Name");
        Assert.Equal("name", name);
    }

    [Fact]
    public void Logo_JsonProperty_IsLogo()
    {
        string? name = GetJsonPropertyName("Logo");
        Assert.Equal("logo", name);
    }

    [Fact]
    public void OriginCountry_JsonProperty_IsOriginCountry()
    {
        string? name = GetJsonPropertyName("OriginCountry");
        Assert.Equal("origin_country", name);
    }

    [Fact]
    public void Description_JsonProperty_IsDescription()
    {
        string? name = GetJsonPropertyName("Description");
        Assert.Equal("description", name);
    }

    [Fact]
    public void Headquarters_JsonProperty_IsHeadquarters()
    {
        string? name = GetJsonPropertyName("Headquarters");
        Assert.Equal("headquarters", name);
    }

    [Fact]
    public void Homepage_JsonProperty_IsHomepage()
    {
        string? name = GetJsonPropertyName("Homepage");
        Assert.Equal("homepage", name);
    }

    [Fact]
    public void Serialization_NetworkTv_UsesNetworkTvKey()
    {
        Network network = new()
        {
            Id = 1,
            Name = "HBO"
        };

        string json = JsonConvert.SerializeObject(network);
        Assert.Contains("\"network_tv\"", json);
        Assert.DoesNotContain("\"network_tv\":1", json);
    }

    [Fact]
    public void Serialization_Id_UsesIdKey()
    {
        Network network = new()
        {
            Id = 42,
            Name = "Netflix"
        };

        string json = JsonConvert.SerializeObject(network);
        Assert.Contains("\"id\":42", json);
    }

    [Fact]
    public void Serialization_NoDuplicateIdKeys()
    {
        Network network = new()
        {
            Id = 1,
            Name = "Test"
        };

        string json = JsonConvert.SerializeObject(network);

        int idCount = 0;
        int index = 0;
        while ((index = json.IndexOf("\"id\"", index, StringComparison.Ordinal)) != -1)
        {
            idCount++;
            index += 4;
        }

        Assert.Equal(1, idCount);
    }

    [Fact]
    public void Deserialization_Id_PopulatesCorrectly()
    {
        string json = """{"id":99,"name":"Test","network_tv":[]}""";
        Network? network = JsonConvert.DeserializeObject<Network>(json);

        Assert.NotNull(network);
        Assert.Equal(99, network.Id);
    }

    [Fact]
    public void Deserialization_RoundTrip_PreservesId()
    {
        Network original = new()
        {
            Id = 55,
            Name = "ABC"
        };

        string json = JsonConvert.SerializeObject(original);
        Network? deserialized = JsonConvert.DeserializeObject<Network>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(55, deserialized.Id);
    }

    [Theory]
    [InlineData("Id", "id")]
    [InlineData("Name", "name")]
    [InlineData("Logo", "logo")]
    [InlineData("OriginCountry", "origin_country")]
    [InlineData("Description", "description")]
    [InlineData("Headquarters", "headquarters")]
    [InlineData("Homepage", "homepage")]
    [InlineData("NetworkTv", "network_tv")]
    public void AllProperties_HaveCorrectJsonPropertyNames(string propertyName, string expectedJsonName)
    {
        string? name = GetJsonPropertyName(propertyName);
        Assert.Equal(expectedJsonName, name);
    }

    [Fact]
    public void NoDuplicateJsonPropertyNames()
    {
        PropertyInfo[] properties = typeof(Network).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        List<string> jsonNames = [];

        foreach (PropertyInfo prop in properties)
        {
            JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
            if (attr?.PropertyName is not null)
            {
                jsonNames.Add(attr.PropertyName);
            }
        }

        int distinctCount = jsonNames.Distinct().Count();
        Assert.Equal(jsonNames.Count, distinctCount);
    }
}
