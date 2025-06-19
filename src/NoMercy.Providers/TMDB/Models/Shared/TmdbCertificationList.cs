using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbCertificationList
{
    [JsonProperty("AU")] public TmdbCertificationItem[] Au { get; set; } = [];
    [JsonProperty("BG")] public TmdbCertificationItem[] Bg { get; set; } = [];
    [JsonProperty("BR")] public TmdbCertificationItem[] Br { get; set; } = [];
    [JsonProperty("CA-QC")] public TmdbCertificationItem[] Caqc { get; set; } = [];
    [JsonProperty("CA")] public TmdbCertificationItem[] Ca { get; set; } = [];
    [JsonProperty("DE")] public TmdbCertificationItem[] De { get; set; } = [];
    [JsonProperty("ES")] public TmdbCertificationItem[] Es { get; set; } = [];
    [JsonProperty("FI")] public TmdbCertificationItem[] Fi { get; set; } = [];
    [JsonProperty("FR")] public TmdbCertificationItem[] Fr { get; set; } = [];
    [JsonProperty("GB")] public TmdbCertificationItem[] Gb { get; set; } = [];
    [JsonProperty("HU")] public TmdbCertificationItem[] Hu { get; set; } = [];
    [JsonProperty("IN")] public TmdbCertificationItem[] In { get; set; } = [];
    [JsonProperty("KR")] public TmdbCertificationItem[] Kr { get; set; } = [];
    [JsonProperty("LT")] public TmdbCertificationItem[] Lt { get; set; } = [];
    [JsonProperty("NL")] public TmdbCertificationItem[] Nl { get; set; } = [];
    [JsonProperty("NZ")] public TmdbCertificationItem[] Nz { get; set; } = [];
    [JsonProperty("PH")] public TmdbCertificationItem[] Ph { get; set; } = [];
    [JsonProperty("RU")] public TmdbCertificationItem[] Ru { get; set; } = [];
    [JsonProperty("SK")] public TmdbCertificationItem[] Sk { get; set; } = [];
    [JsonProperty("US")] public TmdbCertificationItem[] Us { get; set; } = [];
    [JsonProperty("DK")] public TmdbCertificationItem[] Dk { get; set; } = [];
    [JsonProperty("IT")] public TmdbCertificationItem[] It { get; set; } = [];
    [JsonProperty("MY")] public TmdbCertificationItem[] My { get; set; } = [];
    [JsonProperty("NO")] public TmdbCertificationItem[] No { get; set; } = [];
    [JsonProperty("SE")] public TmdbCertificationItem[] Se { get; set; } = [];
    [JsonProperty("TH")] public TmdbCertificationItem[] Th { get; set; } = [];
    [JsonProperty("PT")] public TmdbCertificationItem[] Pt { get; set; } = [];
}