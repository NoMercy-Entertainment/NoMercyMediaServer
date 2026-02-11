using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record TranslationsDto
{
    [JsonProperty("translations")] public TranslationDto[] TranslationsTranslations { get; set; } = [];
}