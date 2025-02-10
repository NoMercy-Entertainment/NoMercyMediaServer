using Newtonsoft.Json;

namespace NoMercy.Providers.Tadb.Models;
public class TadbAlbum
{
    [JsonProperty("idAlbum")] public string IdAlbum { get; set; } = string.Empty;
    [JsonProperty("idArtist")] public string IdArtist { get; set; } = string.Empty;
    [JsonProperty("idLabel")] public string IdLabel { get; set; } = string.Empty;
    [JsonProperty("strAlbum")] public string StrAlbum { get; set; } = string.Empty;
    [JsonProperty("strAlbumStripped")] public string StrAlbumStripped { get; set; } = string.Empty;
    [JsonProperty("strArtist")] public string StrArtist { get; set; } = string.Empty;
    [JsonProperty("strArtistStripped")] public string StrArtistStripped { get; set; } = string.Empty;
    [JsonProperty("intYearReleased")] public string IntYearReleased { get; set; } = string.Empty;
    [JsonProperty("strStyle")] public string StrStyle { get; set; } = string.Empty;
    [JsonProperty("strGenre")] public string StrGenre { get; set; } = string.Empty;
    [JsonProperty("strLabel")] public string StrLabel { get; set; } = string.Empty;
    [JsonProperty("strReleaseFormat")] public string StrReleaseFormat { get; set; } = string.Empty;
    [JsonProperty("intSales")] public string IntSales { get; set; } = string.Empty;
    [JsonProperty("strAlbumThumb")] public string StrAlbumThumb { get; set; } = string.Empty;
    [JsonProperty("strAlbumThumbHQ")] public string StrAlbumThumbHq { get; set; } = string.Empty;
    [JsonProperty("strAlbumThumbBack")] public string StrAlbumThumbBack { get; set; } = string.Empty;
    [JsonProperty("strAlbumCDart")] public string StrAlbumCDart { get; set; } = string.Empty;
    [JsonProperty("strAlbumSpine")] public string StrAlbumSpine { get; set; } = string.Empty;
    [JsonProperty("strAlbum3DCase")] public string StrAlbum3DCase { get; set; } = string.Empty;
    [JsonProperty("strAlbum3DFlat")] public string StrAlbum3DFlat { get; set; } = string.Empty;
    [JsonProperty("strAlbum3DFace")] public string StrAlbum3DFace { get; set; } = string.Empty;
    [JsonProperty("strAlbum3DThumb")] public string StrAlbum3DThumb { get; set; } = string.Empty;
    [JsonProperty("strDescriptionEN")] public string? StrDescriptionEn { get; set; }
    [JsonProperty("strDescriptionDE")] public string? StrDescriptionDe { get; set; }
    [JsonProperty("strDescriptionFR")] public string? StrDescriptionFr { get; set; }
    [JsonProperty("strDescriptionCN")] public string? StrDescriptionCn { get; set; }
    [JsonProperty("strDescriptionIT")] public string? StrDescriptionIt { get; set; }
    [JsonProperty("strDescriptionJP")] public string? StrDescriptionJp { get; set; }
    [JsonProperty("strDescriptionRU")] public string? StrDescriptionRu { get; set; }
    [JsonProperty("strDescriptionES")] public string? StrDescriptionEs { get; set; }
    [JsonProperty("strDescriptionPT")] public string? StrDescriptionPt { get; set; }
    [JsonProperty("strDescriptionSE")] public string? StrDescriptionSe { get; set; }
    [JsonProperty("strDescriptionNL")] public string? StrDescriptionNl { get; set; }
    [JsonProperty("strDescriptionHU")] public string? StrDescriptionHu { get; set; }
    [JsonProperty("strDescriptionNO")] public string? StrDescriptionNo { get; set; }
    [JsonProperty("strDescriptionIL")] public string? StrDescriptionIl { get; set; }
    [JsonProperty("strDescriptionPL")] public string? StrDescriptionPl { get; set; }
    [JsonProperty("intLoved")] public string IntLoved { get; set; } = string.Empty;
    [JsonProperty("intScore")] public string IntScore { get; set; } = string.Empty;
    [JsonProperty("intScoreVotes")] public string IntScoreVotes { get; set; } = string.Empty;
    [JsonProperty("strReview")] public string StrReview { get; set; } = string.Empty;
    [JsonProperty("strMood")] public string StrMood { get; set; } = string.Empty;
    [JsonProperty("strTheme")] public string StrTheme { get; set; } = string.Empty;
    [JsonProperty("strSpeed")] public string StrSpeed { get; set; } = string.Empty;
    [JsonProperty("strLocation")] public string StrLocation { get; set; } = string.Empty;
    [JsonProperty("strMusicBrainzID")] public string StrMusicBrainzId { get; set; } = string.Empty;

    [JsonProperty("strMusicBrainzArtistID")] public string StrMusicBrainzArtistId { get; set; } = string.Empty;

    [JsonProperty("strAllMusicID")] public string StrAllMusicId { get; set; } = string.Empty;
    [JsonProperty("strBBCReviewID")] public string StrBbcReviewId { get; set; } = string.Empty;
    [JsonProperty("strRateYourMusicID")] public string StrRateYourMusicId { get; set; } = string.Empty;
    [JsonProperty("strDiscogsID")] public string StrDiscogsId { get; set; } = string.Empty;
    [JsonProperty("strWikidataID")] public string StrWikidataId { get; set; } = string.Empty;
    [JsonProperty("strWikipediaID")] public string StrWikipediaId { get; set; } = string.Empty;
    [JsonProperty("strGeniusID")] public string StrGeniusId { get; set; } = string.Empty;
    [JsonProperty("strLyricWikiID")] public string StrLyricWikiId { get; set; } = string.Empty;
    [JsonProperty("strMusicMozID")] public string StrMusicMozId { get; set; } = string.Empty;
    [JsonProperty("strItunesID")] public string StrItunesId { get; set; } = string.Empty;
    [JsonProperty("strAmazonID")] public string StrAmazonId { get; set; } = string.Empty;
    [JsonProperty("strLocked")] public string StrLocked { get; set; } = string.Empty;

    [JsonProperty("descriptions")]
    public List<TadbLanguageDescription> Descriptions
    {
        get
        {
            List<TadbLanguageDescription> descriptions = new();
            if (StrDescriptionCn != null)
                descriptions.Add(new() { Iso31661 = "CN", Description = StrDescriptionCn });
            if (StrDescriptionDe != null)
                descriptions.Add(new() { Iso31661 = "DE", Description = StrDescriptionDe });
            if (StrDescriptionEn != null)
                descriptions.Add(new() { Iso31661 = "EN", Description = StrDescriptionEn });
            if (StrDescriptionEs != null)
                descriptions.Add(new() { Iso31661 = "ES", Description = StrDescriptionEs });
            if (StrDescriptionFr != null)
                descriptions.Add(new() { Iso31661 = "FR", Description = StrDescriptionFr });
            if (StrDescriptionHu != null)
                descriptions.Add(new() { Iso31661 = "HU", Description = StrDescriptionHu });
            if (StrDescriptionIl != null)
                descriptions.Add(new() { Iso31661 = "IL", Description = StrDescriptionIl });
            if (StrDescriptionIt != null)
                descriptions.Add(new() { Iso31661 = "IT", Description = StrDescriptionIt });
            if (StrDescriptionJp != null)
                descriptions.Add(new() { Iso31661 = "JP", Description = StrDescriptionJp });
            if (StrDescriptionNl != null)
                descriptions.Add(new() { Iso31661 = "NL", Description = StrDescriptionNl });
            if (StrDescriptionNo != null)
                descriptions.Add(new() { Iso31661 = "NO", Description = StrDescriptionNo });
            if (StrDescriptionPl != null)
                descriptions.Add(new() { Iso31661 = "PL", Description = StrDescriptionPl });
            if (StrDescriptionPt != null)
                descriptions.Add(new() { Iso31661 = "PT", Description = StrDescriptionPt });
            if (StrDescriptionRu != null)
                descriptions.Add(new() { Iso31661 = "RU", Description = StrDescriptionRu });
            if (StrDescriptionSe != null)
                descriptions.Add(new() { Iso31661 = "SE", Description = StrDescriptionSe });

            return descriptions;
        }
    }
}
