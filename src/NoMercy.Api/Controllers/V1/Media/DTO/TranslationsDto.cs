using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record TranslationsDto
{
    [JsonProperty("translations")] public TranslationDto[] TranslationsTranslations { get; set; } = [];
}