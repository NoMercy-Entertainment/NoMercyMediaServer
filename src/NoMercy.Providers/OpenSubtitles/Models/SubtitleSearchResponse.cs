using System.Xml.Serialization;

namespace NoMercy.Providers.OpenSubtitles.Models;

[XmlRoot("methodResponse")]
public class SubtitleSearchResponse
{
    [XmlArray("params")]
    [XmlArrayItem("param")]
    public List<SubtitleSearchResponseParam> Params { get; set; } = new();
}

public class SubtitleSearchResponseParam
{
    [XmlElement("value")]
    public SubtitleSearchResponseMemberValue Value { get; set; } = new();
}

public class SubtitleSearchResponseResponseValue
{
    [XmlElement("struct")]
    public SubtitleSearchResponseStruct Struct { get; set; } = new();
}

public class SubtitleSearchResponseStruct
{
    [XmlElement("member")]
    public List<SubtitleSearchResponseMember> Members { get; set; } = new();
}

public class SubtitleSearchResponseMember
{
    [XmlElement("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("value")]
    public SubtitleSearchResponseMemberValue MemberValue { get; set; } = new();
}

public class SubtitleSearchResponseMemberValue
{
    [XmlElement("struct", IsNullable = true)]
    public SubtitleSearchResponseStruct InnerStruct { get; set; } = new();

    [XmlElement("array", IsNullable = true)]
    public SubtitleSearchResponseArrayData ArrayData { get; set; } = new();

    [XmlElement("string", IsNullable = true)]
    public string StringValue { get; set; } = string.Empty;

    [XmlElement("double", IsNullable = true)]
    public double? DoubleValue { get; set; }

    [XmlElement("int", IsNullable = true)]
    public int? IntValue { get; set; }
}

public class SubtitleSearchResponseArrayData
{
    [XmlArray("data")]
    [XmlArrayItem("value")]
    public List<SubtitleSearchResponseMemberValue> Values { get; set; } = [];
}

// [XmlRoot("methodResponse", IsNullable = false)]
// public class SubtitleSearchResponse
// {
//     [XmlElement("params", IsNullable = false)]
//     public SubtitleSearchResponseParams Params { get; set; } = new();
// }
//
// public class SubtitleSearchResponseParams
// {
//     [XmlElement("param", IsNullable = false)]
//     public SubtitleSearchResponseParam Param { get; set; } = new();
// }
//
// public class SubtitleSearchResponseParam
// {
//     [XmlElement("value", IsNullable = false)]
//     public SubtitleSearchResponseValue Value { get; set; }
// }
//
// public class SubtitleSearchResponseValue
// {
//     [XmlElement("struct", IsNullable = false)]
//     public List<SubtitleSearchResponseStruct> Struct { get; set; } = [];
// }
//
// public class SubtitleSearchResponseStruct
// {
//     [XmlElement("member", IsNullable = false)]
//     public SubtitleSearchResponseMember Members { get; set; } = new();
// }
//
// public class SubtitleSearchResponseMember
// {
//     [XmlElement("name", IsNullable = true)]
//     public string? Name { get; set; }
//     [XmlElement("value", IsNullable = true)]
//     public SubtitleSearchResponseValue? Value { get; set; }
// }
//
// public class Array
// {
//     [XmlElement("data", IsNullable = false)]
//     public SubtitleSearchResponseData Data { get; set; }
// }
//
// public class SubtitleSearchResponseData
// {
//     [XmlElement("values", IsNullable = false)]
//     public List<SubtitleSearchResponseValue> Values { get; set; }
// }
//
// public class QueryParameters
// {
//     [XmlElement("query", IsNullable = true)]
//     public string Query { get; set; }
//     [XmlElement("sublanguageid", IsNullable = true)]
//     public string SubLanguageId { get; set; }
// }
//
// public class DataStruct
// {
//     [XmlElement("matchedby", IsNullable = true)]
//     public string MatchedBy { get; set; }
//     [XmlElement("IDSubtitleFile", IsNullable = true)]
//     public string IDSubMovieFile { get; set; }
//     [XmlElement("MovieHash", IsNullable = true)]
//     public string MovieHash { get; set; }
//     [XmlElement("MovieByteSize", IsNullable = true)]
//     public string MovieByteSize { get; set; }
//     [XmlElement("MovieTimeMS", IsNullable = true)]
//     public string MovieTimeMS { get; set; }
//     [XmlElement("MovieFrames", IsNullable = true)]
//     public string IDSubtitleFile { get; set; }
//     [XmlElement("SubFileName", IsNullable = true)]
//     public string SubFileName { get; set; }
//     [XmlElement("SubActualCD", IsNullable = true)]
//     public string SubActualCD { get; set; }
//     [XmlElement("SubSize", IsNullable = true)]
//     public string SubSize { get; set; }
//     [XmlElement("SubHash", IsNullable = true)]
//     public string SubHash { get; set; }
//     [XmlElement("SubLastTS", IsNullable = true)]
//     public string SubLastTS { get; set; }
//     [XmlElement("SubTSGroup", IsNullable = true)]
//     public string SubTSGroup { get; set; }
//     [XmlElement("InfoReleaseGroup", IsNullable = true)]
//     public string InfoReleaseGroup { get; set; }
//     [XmlElement("InfoFormat", IsNullable = true)]
//     public string InfoFormat { get; set; }
//     [XmlElement("InfoOther", IsNullable = true)]
//     public string InfoOther { get; set; }
//     [XmlElement("IDSubtitle", IsNullable = true)]
//     public string IDSubtitle { get; set; }
//     [XmlElement("UserID", IsNullable = true)]
//     public string UserID { get; set; }
//     [XmlElement("SubLanguageID", IsNullable = true)]
//     public string SubLanguageID { get; set; }
//     [XmlElement("SubFormat", IsNullable = true)]
//     public string SubFormat { get; set; }
//     [XmlElement("SubSumCD", IsNullable = true)]
//     public string SubSumCD { get; set; }
//     [XmlElement("SubAuthorComment", IsNullable = true)]
//     public string SubAuthorComment { get; set; }
//     [XmlElement("SubAddDate", IsNullable = true)]
//     public string SubAddDate { get; set; }
//     [XmlElement("SubBad", IsNullable = true)]
//     public string SubBad { get; set; }
//     [XmlElement("SubRating", IsNullable = true)]
//     public string SubRating { get; set; }
//     [XmlElement("SubSumVotes", IsNullable = true)]
//     public string SubSumVotes { get; set; }
//     [XmlElement("SubDownloadsCnt", IsNullable = true)]
//     public string SubDownloadsCnt { get; set; }
//     [XmlElement("MovieReleaseName", IsNullable = true)]
//     public string MovieReleaseName { get; set; }
//     [XmlElement("MovieFPS", IsNullable = true)]
//     public string MovieFPS { get; set; }
//     [XmlElement("IDMovie", IsNullable = true)]
//     public string IDMovie { get; set; }
//     [XmlElement("IDMovieImdb", IsNullable = true)]
//     public string IDMovieImdb { get; set; }
//     [XmlElement("MovieName", IsNullable = true)]
//     public string MovieName { get; set; }
//     [XmlElement("MovieNameEng", IsNullable = true)]
//     public string MovieNameEng { get; set; }
//     [XmlElement("MovieYear", IsNullable = true)]
//     public string MovieYear { get; set; }
//     [XmlElement("MovieImdbRating", IsNullable = true)]
//     public string MovieImdbRating { get; set; }
//     [XmlElement("SubFeatured", IsNullable = true)]
//     public string SubFeatured { get; set; }
//     [XmlElement("UserNickName", IsNullable = true)]
//     public string UserNickName { get; set; }
//     [XmlElement("SubTranslator", IsNullable = true)]
//     public string SubTranslator { get; set; }
//     [XmlElement("ISO639", IsNullable = true)]
//     public string ISO639 { get; set; }
//     [XmlElement("LanguageName", IsNullable = true)]
//     public string LanguageName { get; set; }
//     [XmlElement("SubComments", IsNullable = true)]
//     public string SubComments { get; set; }
//     [XmlElement("SubHearingImpaired", IsNullable = true)]
//     public string SubHearingImpaired { get; set; }
//     [XmlElement("UserRank", IsNullable = true)]
//     public string UserRank { get; set; }
//     [XmlElement("SeriesSeason", IsNullable = true)]
//     public string SeriesSeason { get; set; }
//     [XmlElement("SeriesEpisode", IsNullable = true)]
//     public string SeriesEpisode { get; set; }
//     [XmlElement("MovieKind", IsNullable = true)]
//     public string MovieKind { get; set; }
//     [XmlElement("SubHD", IsNullable = true)]
//     public string SubHD { get; set; }
//     [XmlElement("SeriesIMDBParent", IsNullable = true)]
//     public string SeriesIMDBParent { get; set; }
//     [XmlElement("SubEncoding", IsNullable = true)]
//     public string SubEncoding { get; set; }
//     [XmlElement("SubAutoTranslation", IsNullable = true)]
//     public string SubAutoTranslation { get; set; }
//     [XmlElement("SubForeignParts", IsNullable = true)]
//     public string SubForeignPartsOnly { get; set; }
//     [XmlElement("SubFromTrusted", IsNullable = true)]
//     public string SubFromTrusted { get; set; }
//     [XmlElement("QueryParameters", IsNullable = true)]
//     public QueryParameters QueryParameters { get; set; }
//     [XmlElement("QueryCached", IsNullable = true)]
//     public int QueryCached { get; set; }
//     [XmlElement("SubTSGroupHash", IsNullable = true)]
//     public string SubTSGroupHash { get; set; }
//     [XmlElement("SubDownloadLink", IsNullable = true)]
//     public string SubDownloadLink { get; set; } 
//     [XmlElement("ZipDownloadLink", IsNullable = true)]
//     public string ZipDownloadLink { get; set; }
//     [XmlElement("SubtitlesLink", IsNullable = true)]
//     public string SubtitlesLink { get; set; }
//     [XmlElement("QueryNumber", IsNullable = true)]
//     public string QueryNumber { get; set; }
//     [XmlElement("Score", IsNullable = true)]
//     public double Score { get; set; }
// }