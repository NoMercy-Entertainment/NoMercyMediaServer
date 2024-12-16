using CommandLine;
using Serilog.Events;

namespace NoMercy.Server.Cli;

// ReSharper disable once ClassNeverInstantiated.Global
public class CliOptions
{
    #region CLI Options
    [Option('d', "dev", Required = false, HelpText = "Run the server in development mode.")]
    public bool Dev { get; set; }
    
    [Option('l', "loglevel", Required = false, HelpText = "Run the server in development mode.")]
    public string LogLevel { get; set; } = LogEventLevel.Information.ToString();
    
    [Option("seed", Required = false, HelpText = "Run the server in development mode.")]
    public bool Seed { get; set; }
    
    [Option('i', "internal-port", Required = false, HelpText = "Internal port to use for the server.")]
    public int InternalPort { get; set; }
    
    [Option('e', "external-port", Required = false, HelpText = "External port to use for the server.")]
    public int ExternalPort { get; set; }
    #endregion
    
    public bool ShouldSeedMarvel { get; set; } = false; // Set by the parser as a navigation property
}