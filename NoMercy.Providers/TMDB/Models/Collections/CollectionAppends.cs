﻿using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Combined;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Providers.TMDB.Models.Collections;

public class CollectionAppends: CollectionDetails
{
    [JsonProperty("images")] public CollectionImages Images { get; set; }
    [JsonProperty("translations")] public CombinedTranslations Translations { get; set; }
}
