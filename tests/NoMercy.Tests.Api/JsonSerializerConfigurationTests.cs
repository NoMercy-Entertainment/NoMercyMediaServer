using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class JsonSerializerConfigurationTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public JsonSerializerConfigurationTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void NewtonsoftJson_HasStringEnumConverter_Configured()
    {
        MvcNewtonsoftJsonOptions options =
            _factory.Services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value;

        bool hasStringEnumConverter = options.SerializerSettings.Converters
            .Any(c => c is StringEnumConverter);

        Assert.True(hasStringEnumConverter,
            "Newtonsoft.Json controller settings must include StringEnumConverter for enum-as-string serialization");
    }

    [Fact]
    public void NewtonsoftJson_HasReferenceLoopHandling_Ignore()
    {
        MvcNewtonsoftJsonOptions options =
            _factory.Services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value;

        Assert.Equal(ReferenceLoopHandling.Ignore, options.SerializerSettings.ReferenceLoopHandling);
    }

    [Fact]
    public void NewtonsoftJson_HasUtcDateTimeZoneHandling()
    {
        MvcNewtonsoftJsonOptions options =
            _factory.Services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value;

        Assert.Equal(DateTimeZoneHandling.Utc, options.SerializerSettings.DateTimeZoneHandling);
    }

    [Fact]
    public void NewtonsoftJson_HasIsoDateFormatHandling()
    {
        MvcNewtonsoftJsonOptions options =
            _factory.Services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value;

        Assert.Equal(DateFormatHandling.IsoDateFormat, options.SerializerSettings.DateFormatHandling);
    }

    [Fact]
    public void SystemTextJson_IsNotConfigured_AsDuplicateSerializer()
    {
        JsonOptions options =
            _factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value;

        bool hasJsonStringEnumConverter = options.JsonSerializerOptions.Converters
            .Any(c => c.GetType().Name == "JsonStringEnumConverter");

        Assert.False(hasJsonStringEnumConverter,
            "System.Text.Json should not have JsonStringEnumConverter configured — " +
            "Newtonsoft.Json is the sole controller serializer and handles enum conversion via StringEnumConverter");
    }
}
