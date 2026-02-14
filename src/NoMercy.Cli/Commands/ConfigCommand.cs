using System.CommandLine;
using System.Text;
using Newtonsoft.Json;
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class ConfigCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command getCmd = new("get")
        {
            Description = "Show current configuration"
        };

        getCmd.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            using CliClient client = new(pipe);
            ConfigResponse? config = await client.GetAsync<ConfigResponse>(
                "/manage/config", ct);

            if (config is null)
            {
                Console.Error.WriteLine("Could not connect to server.");
                return 1;
            }

            Console.WriteLine($"Server Name:      {config.ServerName}");
            Console.WriteLine($"Internal Port:    {config.InternalPort}");
            Console.WriteLine($"External Port:    {config.ExternalPort}");
            Console.WriteLine($"Queue Workers:    {config.QueueWorkers}");
            Console.WriteLine($"Encoder Workers:  {config.EncoderWorkers}");
            Console.WriteLine($"Cron Workers:     {config.CronWorkers}");
            Console.WriteLine($"Data Workers:     {config.DataWorkers}");
            Console.WriteLine($"Image Workers:    {config.ImageWorkers}");
            Console.WriteLine($"File Workers:     {config.FileWorkers}");
            Console.WriteLine($"Request Workers:  {config.RequestWorkers}");
            Console.WriteLine($"Swagger:          {config.Swagger}");
            return 0;
        });

        Argument<string> keyArg = new("key")
        {
            Description = "Configuration key to set"
        };
        Argument<string> valArg = new("value")
        {
            Description = "Value to set"
        };

        Command setCmd = new("set")
        {
            Description = "Update a configuration value"
        };
        setCmd.Arguments.Add(keyArg);
        setCmd.Arguments.Add(valArg);

        setCmd.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            string key = parseResult.GetValue(keyArg)!;
            string val = parseResult.GetValue(valArg)!;

            using CliClient client = new(pipe);

            Dictionary<string, object> payload = new() { { ToSnakeCase(key), ParseValue(val) } };
            string json = JsonConvert.SerializeObject(payload);
            StringContent content = new(json, Encoding.UTF8, "application/json");

            bool ok = await client.PutAsync("/manage/config", content, ct);

            if (ok)
            {
                Console.WriteLine($"Configuration updated: {key} = {val}");
                return 0;
            }

            return 1;
        });

        Command command = new("config")
        {
            Description = "Manage server configuration"
        };
        command.Subcommands.Add(getCmd);
        command.Subcommands.Add(setCmd);

        return command;
    }

    internal static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        StringBuilder sb = new();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '-' || c == '_')
            {
                sb.Append('_');
                continue;
            }
            if (char.IsUpper(c) && i > 0 && input[i - 1] != '_' && input[i - 1] != '-')
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static object ParseValue(string val)
    {
        if (int.TryParse(val, out int intVal)) return intVal;
        if (bool.TryParse(val, out bool boolVal)) return boolVal;
        return val;
    }
}
