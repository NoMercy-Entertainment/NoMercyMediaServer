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

using Newtonsoft.Json;

namespace NoMercy.Providers.Tadb.Models;

public class TadbAlbum
{
    [JsonProperty(propertyName: "idAlbum")]
    public string IdAlbum { get; set; } = string.Empty;

    [JsonProperty(propertyName: "idArtist")]
    public string IdArtist { get; set; } = string.Empty;

    [JsonProperty(propertyName: "idLabel")]
    public string IdLabel { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbum")]
    public string StrAlbum { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbumStripped")]
    public string StrAlbumStripped { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strArtist")]
    public string StrArtist { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strArtistStripped")]
    public string StrArtistStripped { get; set; } = string.Empty;

    [JsonProperty(propertyName: "intYearReleased")]
    public string IntYearReleased { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strStyle")]
    public string StrStyle { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strGenre")]
    public string StrGenre { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strLabel")]
    public string StrLabel { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strReleaseFormat")]
    public string StrReleaseFormat { get; set; } = string.Empty;

    [JsonProperty(propertyName: "intSales")]
    public string IntSales { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbumThumb")]
    public string StrAlbumThumb { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbumThumbHQ")]
    public string StrAlbumThumbHq { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbumThumbBack")]
    public string StrAlbumThumbBack { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbumCDart")]
    public string StrAlbumCDart { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbumSpine")]
    public string StrAlbumSpine { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbum3DCase")]
    public string StrAlbum3DCase { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbum3DFlat")]
    public string StrAlbum3DFlat { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbum3DFace")]
    public string StrAlbum3DFace { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAlbum3DThumb")]
    public string StrAlbum3DThumb { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strDescriptionEN")]
    public string? StrDescriptionEn { get; set; }

    [JsonProperty(propertyName: "strDescriptionDE")]
    public string? StrDescriptionDe { get; set; }

    [JsonProperty(propertyName: "strDescriptionFR")]
    public string? StrDescriptionFr { get; set; }

    [JsonProperty(propertyName: "strDescriptionCN")]
    public string? StrDescriptionCn { get; set; }

    [JsonProperty(propertyName: "strDescriptionIT")]
    public string? StrDescriptionIt { get; set; }

    [JsonProperty(propertyName: "strDescriptionJP")]
    public string? StrDescriptionJp { get; set; }

    [JsonProperty(propertyName: "strDescriptionRU")]
    public string? StrDescriptionRu { get; set; }

    [JsonProperty(propertyName: "strDescriptionES")]
    public string? StrDescriptionEs { get; set; }

    [JsonProperty(propertyName: "strDescriptionPT")]
    public string? StrDescriptionPt { get; set; }

    [JsonProperty(propertyName: "strDescriptionSE")]
    public string? StrDescriptionSe { get; set; }

    [JsonProperty(propertyName: "strDescriptionNL")]
    public string? StrDescriptionNl { get; set; }

    [JsonProperty(propertyName: "strDescriptionHU")]
    public string? StrDescriptionHu { get; set; }

    [JsonProperty(propertyName: "strDescriptionNO")]
    public string? StrDescriptionNo { get; set; }

    [JsonProperty(propertyName: "strDescriptionIL")]
    public string? StrDescriptionIl { get; set; }

    [JsonProperty(propertyName: "strDescriptionPL")]
    public string? StrDescriptionPl { get; set; }

    [JsonProperty(propertyName: "intLoved")]
    public string IntLoved { get; set; } = string.Empty;

    [JsonProperty(propertyName: "intScore")]
    public string IntScore { get; set; } = string.Empty;

    [JsonProperty(propertyName: "intScoreVotes")]
    public string IntScoreVotes { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strReview")]
    public string StrReview { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strMood")]
    public string StrMood { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strTheme")]
    public string StrTheme { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strSpeed")]
    public string StrSpeed { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strLocation")]
    public string StrLocation { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strMusicBrainzID")]
    public string StrMusicBrainzId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strMusicBrainzArtistID")]
    public string StrMusicBrainzArtistId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAllMusicID")]
    public string StrAllMusicId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strBBCReviewID")]
    public string StrBbcReviewId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strRateYourMusicID")]
    public string StrRateYourMusicId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strDiscogsID")]
    public string StrDiscogsId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strWikidataID")]
    public string StrWikidataId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strWikipediaID")]
    public string StrWikipediaId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strGeniusID")]
    public string StrGeniusId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strLyricWikiID")]
    public string StrLyricWikiId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strMusicMozID")]
    public string StrMusicMozId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strItunesID")]
    public string StrItunesId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strAmazonID")]
    public string StrAmazonId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "strLocked")]
    public string StrLocked { get; set; } = string.Empty;

    [JsonProperty(propertyName: "descriptions")]
    public List<TadbLanguageDescription> Descriptions
    {
        get
        {
            List<TadbLanguageDescription> descriptions = [];
            if (StrDescriptionCn != null)
                descriptions.Add(item: new() { Iso31661 = "CN", Description = StrDescriptionCn });
            if (StrDescriptionDe != null)
                descriptions.Add(item: new() { Iso31661 = "DE", Description = StrDescriptionDe });
            if (StrDescriptionEn != null)
                descriptions.Add(item: new() { Iso31661 = "EN", Description = StrDescriptionEn });
            if (StrDescriptionEs != null)
                descriptions.Add(item: new() { Iso31661 = "ES", Description = StrDescriptionEs });
            if (StrDescriptionFr != null)
                descriptions.Add(item: new() { Iso31661 = "FR", Description = StrDescriptionFr });
            if (StrDescriptionHu != null)
                descriptions.Add(item: new() { Iso31661 = "HU", Description = StrDescriptionHu });
            if (StrDescriptionIl != null)
                descriptions.Add(item: new() { Iso31661 = "IL", Description = StrDescriptionIl });
            if (StrDescriptionIt != null)
                descriptions.Add(item: new() { Iso31661 = "IT", Description = StrDescriptionIt });
            if (StrDescriptionJp != null)
                descriptions.Add(item: new() { Iso31661 = "JP", Description = StrDescriptionJp });
            if (StrDescriptionNl != null)
                descriptions.Add(item: new() { Iso31661 = "NL", Description = StrDescriptionNl });
            if (StrDescriptionNo != null)
                descriptions.Add(item: new() { Iso31661 = "NO", Description = StrDescriptionNo });
            if (StrDescriptionPl != null)
                descriptions.Add(item: new() { Iso31661 = "PL", Description = StrDescriptionPl });
            if (StrDescriptionPt != null)
                descriptions.Add(item: new() { Iso31661 = "PT", Description = StrDescriptionPt });
            if (StrDescriptionRu != null)
                descriptions.Add(item: new() { Iso31661 = "RU", Description = StrDescriptionRu });
            if (StrDescriptionSe != null)
                descriptions.Add(item: new() { Iso31661 = "SE", Description = StrDescriptionSe });

            return descriptions;
        }
    }
}
