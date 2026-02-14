using System.CommandLine;
using Newtonsoft.Json;

namespace NoMercy.Cli.Commands;

internal static class UpdateCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command command = new("update")
        {
            Description = "Apply a downloaded server update"
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            using CliClient client = new(pipe);
            UpdateResponse? response = await client.PostAsync<UpdateResponse>(
                "/manage/update", null, ct);

            if (response is null)
                return 1;

            Console.WriteLine(response.Message);
            return response.Status == "ok" ? 0 : 1;
        });

        return command;
    }

    private class UpdateResponse
    {
        [JsonProperty("status")] public string Status { get; set; } = string.Empty;
        [JsonProperty("message")] public string Message { get; set; } = string.Empty;
    }
}
