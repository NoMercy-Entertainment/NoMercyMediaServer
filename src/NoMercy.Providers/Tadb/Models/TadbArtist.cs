using Newtonsoft.Json;

namespace NoMercy.Providers.Tadb.Models;
public class TadbArtist
{
    [JsonProperty("idArtist")] public string? IdArtist { get; set; }
    [JsonProperty("strArtist")] public string? StrArtist { get; set; }
    [JsonProperty("strArtistStripped")] public string? StrArtistStripped { get; set; }
    [JsonProperty("strArtistAlternate")] public string? StrArtistAlternate { get; set; }
    [JsonProperty("strLabel")] public string? StrLabel { get; set; }
    [JsonProperty("idLabel")] public string? IdLabel { get; set; }
    [JsonProperty("intFormedYear")] public string? IntFormedYear { get; set; }
    [JsonProperty("intBornYear")] public string? IntBornYear { get; set; }
    [JsonProperty("intDiedYear")] public string? IntDiedYear { get; set; }
    [JsonProperty("strDisbanded")] public string? StrDisbanded { get; set; }
    [JsonProperty("strStyle")] public string? StrStyle { get; set; }
    [JsonProperty("strGenre")] public string? StrGenre { get; set; }
    [JsonProperty("strMood")] public string? StrMood { get; set; }
    [JsonProperty("strWebsite")] public string? StrWebsite { get; set; }
    [JsonProperty("strFacebook")] public string? StrFacebook { get; set; }
    [JsonProperty("strTwitter")] public string? StrTwitter { get; set; }
    [JsonProperty("strBiographyEN")] public string? StrBiographyEN { get; set; }
    [JsonProperty("strBiographyDE")] public string? StrBiographyDE { get; set; }
    [JsonProperty("strBiographyFR")] public string? StrBiographyFR { get; set; }
    [JsonProperty("strBiographyCN")] public string? StrBiographyCN { get; set; }
    [JsonProperty("strBiographyIT")] public string? StrBiographyIT { get; set; }
    [JsonProperty("strBiographyJP")] public string? StrBiographyJP { get; set; }
    [JsonProperty("strBiographyRU")] public string? StrBiographyRU { get; set; }
    [JsonProperty("strBiographyES")] public string? StrBiographyES { get; set; }
    [JsonProperty("strBiographyPT")] public string? StrBiographyPT { get; set; }
    [JsonProperty("strBiographySE")] public string? StrBiographySE { get; set; }
    [JsonProperty("strBiographyNL")] public string? StrBiographyNL { get; set; }
    [JsonProperty("strBiographyHU")] public string? StrBiographyHU { get; set; }
    [JsonProperty("strBiographyNO")] public string? StrBiographyNO { get; set; }
    [JsonProperty("strBiographyIL")] public string? StrBiographyIL { get; set; }
    [JsonProperty("strBiographyPL")] public string? StrBiographyPL { get; set; }
    [JsonProperty("strGender")] public string? StrGender { get; set; }
    [JsonProperty("intMembers")] public string? IntMembers { get; set; }
    [JsonProperty("strCountry")] public string? StrCountry { get; set; }
    [JsonProperty("strCountryCode")] public string? StrCountryCode { get; set; }
    [JsonProperty("strArtistThumb")] public string? StrArtistThumb { get; set; }
    [JsonProperty("strArtistLogo")] public string? StrArtistLogo { get; set; }
    [JsonProperty("strArtistCutout")] public string? StrArtistCutout { get; set; }
    [JsonProperty("strArtistClearart")] public string? StrArtistClearart { get; set; }
    [JsonProperty("strArtistWideThumb")] public string? StrArtistWideThumb { get; set; }
    [JsonProperty("strArtistFanart")] public string? StrArtistFanart { get; set; }
    [JsonProperty("strArtistFanart2")] public string? StrArtistFanart2 { get; set; }
    [JsonProperty("strArtistFanart3")] public string? StrArtistFanart3 { get; set; }
    [JsonProperty("strArtistFanart4")] public string? StrArtistFanart4 { get; set; }
    [JsonProperty("strArtistBanner")] public string? StrArtistBanner { get; set; }
    [JsonProperty("strMusicBrainzID")] public string? StrMusicBrainzId { get; set; }
    [JsonProperty("strISNIcode")] public string? StrIsnIcode { get; set; }
    [JsonProperty("strLastFMChart")] public string? StrLastFmChart { get; set; }
    [JsonProperty("intCharted")] public string? IntCharted { get; set; }
    [JsonProperty("strLocked")] public string? StrLocked { get; set; }

    [JsonProperty("descriptions")]
    public List<TadbLanguageDescription> Descriptions
    {
        get
        {
            List<TadbLanguageDescription> descriptions = new();
            if (StrBiographyCN != null)
                descriptions.Add(new() { Iso31661 = "CN", Description = StrBiographyCN });
            if (StrBiographyDE != null)
                descriptions.Add(new() { Iso31661 = "DE", Description = StrBiographyDE });
            if (StrBiographyEN != null)
                descriptions.Add(new() { Iso31661 = "EN", Description = StrBiographyEN });
            if (StrBiographyES != null)
                descriptions.Add(new() { Iso31661 = "ES", Description = StrBiographyES });
            if (StrBiographyFR != null)
                descriptions.Add(new() { Iso31661 = "FR", Description = StrBiographyFR });
            if (StrBiographyHU != null)
                descriptions.Add(new() { Iso31661 = "HU", Description = StrBiographyHU });
            if (StrBiographyIL != null)
                descriptions.Add(new() { Iso31661 = "IL", Description = StrBiographyIL });
            if (StrBiographyIT != null)
                descriptions.Add(new() { Iso31661 = "IT", Description = StrBiographyIT });
            if (StrBiographyJP != null)
                descriptions.Add(new() { Iso31661 = "JP", Description = StrBiographyJP });
            if (StrBiographyNL != null)
                descriptions.Add(new() { Iso31661 = "NL", Description = StrBiographyNL });
            if (StrBiographyNO != null)
                descriptions.Add(new() { Iso31661 = "NO", Description = StrBiographyNO });
            if (StrBiographyPL != null)
                descriptions.Add(new() { Iso31661 = "PL", Description = StrBiographyPL });
            if (StrBiographyPT != null)
                descriptions.Add(new() { Iso31661 = "PT", Description = StrBiographyPT });
            if (StrBiographyRU != null)
                descriptions.Add(new() { Iso31661 = "RU", Description = StrBiographyRU });
            if (StrBiographySE != null)
                descriptions.Add(new() { Iso31661 = "SE", Description = StrBiographySE });

            return descriptions;
        }
    }
}
