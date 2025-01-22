using NoMercy.Providers.OpenSubtitles.Models;

namespace NoMercy.Providers.OpenSubtitles.Client;

public class OpenSubtitlesClient : OpenSubtitlesBaseClient
{
    public async Task<OpenSubtitlesClient> Login()
    {
        Login login = new()
        {
            MethodName = "LogIn",
            Params =
            [
                new()
                {
                    Value = new()
                    {
                        String = ""
                    }
                },
                new()
                {
                    Value = new()
                    {
                        String = ""
                    }
                },
                new()
                {
                    Value = new()
                    {
                        String = "dut"
                    }
                },
                new()
                {
                    Value = new()
                    {
                        // String = ApiInfo.UserAgent
                        String = "VLSub"
                    }
                }
            ]
        };

        var x = await  Post<Login, LoginResponse>("", login);
        AccessToken = x?.Params?.Param?.Value?.Struct?.Member.FirstOrDefault(member => member.Name == "token")?.Value?.String;
        
        return this;
    }
    
    public async Task<SubtitleSearchResponse?> SearchSubtitles(string query, string language)
    {
        SubtitleSearch searchResponse = new()
        {
            MethodCall = new()
            {
                MethodName = "SearchSubtitles",
                Params = new()
                {
                    Param =
                    [
                        new()
                        {
                            Value = new()
                            {
                                String = AccessToken!
                            }
                        },
                        new()
                        {
                            Value = new()
                            {
                                Array = new()
                                {
                                    Data = new()
                                    {
                                        Value = new()
                                        {
                                            Struct = new()
                                            {
                                                Member =
                                                [
                                                    new(name: "sublanguageid", value: new()
                                                    {
                                                        String = language
                                                    }),
                                                    new(name: "query", value: new()
                                                    {
                                                        String = query
                                                    }),
                                                ]
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    ]
                }
            }
        };

        return await Post<SubtitleSearch, SubtitleSearchResponse>("", searchResponse);
    }
}