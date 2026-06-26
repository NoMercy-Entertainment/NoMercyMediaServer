namespace NoMercy.Setup.Server;

public interface IApiKeyStore
{
    string AcousticIdKey { get; }
    string FanArtApiKey { get; }
    string FanArtClientKey { get; }
    string JwplayerKey { get; }
    string MakeMkvKey { get; }
    string MusixmatchKey { get; }
    string OmdbKey { get; }
    string RottenTomatoes { get; }
    string TadbKey { get; }
    string TmdbKey { get; }
    string TmdbToken { get; }
    string TvdbKey { get; }
    bool KeysLoaded { get; }
    string[] Colors { get; }
    string Quote { get; }
}
