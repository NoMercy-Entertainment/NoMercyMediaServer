using System.CommandLine;
using System.Text;
using Newtonsoft.Json;

namespace NoMercy.Cli.Commands;

internal static class AutoStartCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command statusCmd = new("status")
        {
            Description = "Check if autostart is enabled"
        };

        statusCmd.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            using CliClient client = new(pipe);
            AutoStartResponse? response = await client.GetAsync<AutoStartResponse>(
                "/manage/autostart", ct);

            if (response is null)
            {
                Console.Error.WriteLine("Could not retrieve autostart status.");
                return 1;
            }

            Console.WriteLine($"Autostart:    {(response.Enabled ? "enabled" : "disabled")}");
            return 0;
        });

        Command enableCmd = new("enable")
        {
            Description = "Enable autostart"
        };

        enableCmd.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            return await SetAutoStart(parseResult, pipeOption, true, ct);
        });

        Command disableCmd = new("disable")
        {
            Description = "Disable autostart"
        };

        disableCmd.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            return await SetAutoStart(parseResult, pipeOption, false, ct);
        });

        Command command = new("autostart")
        {
            Description = "Manage server autostart"
        };
        command.Subcommands.Add(statusCmd);
        command.Subcommands.Add(enableCmd);
        command.Subcommands.Add(disableCmd);

        return command;
    }

    private static async Task<int> SetAutoStart(
        ParseResult parseResult,
        Option<string?> pipeOption,
        bool enabled,
        CancellationToken ct)
    {
        string? pipe = parseResult.GetValue(pipeOption);
        using CliClient client = new(pipe);

        string json = JsonConvert.SerializeObject(new { enabled });
        StringContent content = new(json, Encoding.UTF8, "application/json");

        bool ok = await client.PostAsync("/manage/autostart", content, ct);

        if (ok)
        {
            Console.WriteLine($"Autostart {(enabled ? "enabled" : "disabled")}.");
            return 0;
        }

        return 1;
    }

    private class AutoStartResponse
    {
        [JsonProperty("enabled")] public bool Enabled { get; set; }
    }
}
