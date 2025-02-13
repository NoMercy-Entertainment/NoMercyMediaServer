using NoMercy.Networking;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Providers.Other;

public static class KitsuIo
{
    public static async Task<bool> IsAnime(string title, int year)
    {
        bool isAnime = false;

        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);
        client.BaseAddress = new("https://kitsu.io/api/edge/");

        HttpResponseMessage response = await client.GetAsync($"anime?filter[text]={title}&filter[year]={year}");
        string content = await response.Content.ReadAsStringAsync();

        try
        {
            KitsuAnime? anime = content.FromJson<KitsuAnime>();

            foreach (Data data in anime?.Data ?? [])
                if (data.Attributes.Titles.En?.Equals(title, StringComparison.CurrentCultureIgnoreCase) == true)
                    isAnime = true;
                else if (data.Attributes.Titles.EnJp?.Equals(title, StringComparison.CurrentCultureIgnoreCase) == true)
                    isAnime = true;
                else if (data.Attributes.Titles.JaJp?.Equals(title, StringComparison.CurrentCultureIgnoreCase) == true)
                    isAnime = true;
                else if (data.Attributes.Titles.ThTh?.Equals(title, StringComparison.CurrentCultureIgnoreCase) == true)
                    isAnime = true;
                else if (data.Attributes.AbbreviatedTitles.Any(abbreviatedTitle =>
                             abbreviatedTitle.Equals(title, StringComparison.CurrentCultureIgnoreCase)))
                    isAnime = true;
        }
        catch (Exception e)
        {
            Logger.AniDb(e.Message, LogEventLevel.Fatal);
        }

        return isAnime;
    }
}