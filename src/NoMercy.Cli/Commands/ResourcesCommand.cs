using System.CommandLine;
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class ResourcesCommand
{
    public static Command Create(Option<string?> pipeOption)
    {
        Command command = new("resources")
        {
            Description = "Show server resource usage"
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            string? pipe = parseResult.GetValue(pipeOption);
            using CliClient client = new(pipe);
            ResourcesResponse? resources = await client.GetAsync<ResourcesResponse>(
                "/manage/resources", ct);

            if (resources is null)
            {
                Console.Error.WriteLine("Could not retrieve resource information.");
                return 1;
            }

            Console.WriteLine($"CPU:          {resources.Cpu.Total:F1}% (max {resources.Cpu.Max:F1}%)");

            Console.WriteLine($"Memory:       {resources.Memory.Use:F1} / {resources.Memory.Total:F1} GB ({resources.Memory.Percentage:F1}%)");

            if (resources.Gpu.Count > 0)
            {
                foreach (GpuInfo gpu in resources.Gpu)
                {
                    Console.WriteLine($"GPU {gpu.Index}:        {gpu.Core:F1}% core, {gpu.Memory:F1}% memory, {gpu.Encode:F1}% encode, {gpu.Decode:F1}% decode");
                }
            }

            if (resources.Storage.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Storage:");
                foreach (StorageInfo drive in resources.Storage)
                {
                    double used = drive.Total - drive.Available;
                    Console.WriteLine($"  {drive.Name,-12} {used:F1} / {drive.Total:F1} GB ({drive.Percentage:F1}% free)");
                }
            }

            return 0;
        });

        return command;
    }
}
