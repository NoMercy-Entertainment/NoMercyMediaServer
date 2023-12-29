﻿using Newtonsoft.Json;

namespace NoMercy.TMDBApi.Models.Shared;

public class Poster
{
    [JsonProperty("aspect_ratio")] public double AspectRatio { get; set; }

    [JsonProperty("file_path")] public string FilePath { get; set; }

    [JsonProperty("height")] public int Height { get; set; }

    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; }

    [JsonProperty("vote_average")] public double VoteAverage { get; set; }

    [JsonProperty("vote_count")] public int VoteCount { get; set; }

    [JsonProperty("width")] public int Width { get; set; }
}