using System.Text.RegularExpressions;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.EncoderV2.Capabilities;

public class CapabilityGenerator
{
    private readonly string _ffmpegPath;

    public CapabilityGenerator(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath;
    }

    public async Task<AllCapabilities> GenerateCapabilities()
    {
        Console.WriteLine($"[GenerateCapabilities] Called");
        AllCapabilities capabilities = new();

        try
        {
            Console.WriteLine($"[GenerateCapabilities] Fetching encoders...");
            List<(string name, string type)> encoders = await GetEncoders();
            Console.WriteLine($"[GenerateCapabilities] Found {encoders.Count} encoders");
            
            foreach ((string name, string type) in encoders)
            {
                try
                {
                    Dictionary<string, EncoderOption> options = await GetEncoderOptions(name);
                    EncoderCapability capability = new()
                    {
                        Name = name,
                        LongName = name,
                        Type = type,
                        IsHardware = name.Contains("nvenc") || name.Contains("qsv") || name.Contains("videotoolbox"),
                        Options = options
                    };

                    switch (type)
                    {
                        case "V":
                            capabilities.VideoEncoders[name] = capability;
                            break;
                        case "A":
                            capabilities.AudioEncoders[name] = capability;
                            break;
                        case "S":
                            capabilities.SubtitleEncoders[name] = capability;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GenerateCapabilities] Error processing encoder {name}: {ex.Message}");
                }
            }

            Console.WriteLine($"[GenerateCapabilities] Fetching containers...");
            List<string> containers = await GetContainers();
            Console.WriteLine($"[GenerateCapabilities] Found {containers.Count} containers");
            
            foreach (string name in containers)
            {
                ContainerCapability container = new()
                {
                    Name = name,
                    LongName = name,
                    CanMux = true,
                    CanDemux = true
                };
                capabilities.Containers[name] = container;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenerateCapabilities] Error: {ex.Message}\n{ex.StackTrace}");
        }

        Console.WriteLine($"[GenerateCapabilities] Complete: {capabilities.VideoEncoders.Count} video, {capabilities.AudioEncoders.Count} audio, {capabilities.SubtitleEncoders.Count} subtitle, {capabilities.Containers.Count} containers");
        return capabilities;
    }

    private async Task<List<(string name, string type)>> GetEncoders()
    {
        List<(string, string)> encoders = new();
        try
        {
            Console.WriteLine($"[GetEncoders] Called");
            string output = await RunFfmpeg("-encoders");
            Console.WriteLine($"[GetEncoders] Received {output.Length} chars");
            
            if (string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"[GetEncoders] Output is empty!");
                return encoders;
            }

            // Split by newline and clean up lines
            string[] lines = output.Split('\n');
            Console.WriteLine($"[GetEncoders] Split into {lines.Length} lines");
            Console.WriteLine($"[GetEncoders] Split into {lines.Length} lines");
            
            bool inEncoders = false;
            int parsedCount = 0;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim('\r');  // Remove carriage returns
                
                // Skip until we find the separator line (dashes)
                if (line.Trim() == "------" || line == " ------")
                {
                    inEncoders = true;
                    Console.WriteLine($"[GetEncoders] Found separator line");
                    continue;
                }

                if (!inEncoders) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Format: " V....D encoder_name    Description"
                // Skip lines that don't start with space (for robustness)
                if (!line.StartsWith(" ")) continue;
                
                // Trim leading space to make parsing easier
                string trimmed = line.Substring(1);
                if (trimmed.Length < 6) continue;

                // Extract type flags (first 6 characters)
                string typeFlags = trimmed.Substring(0, 6);
                string rest = trimmed.Substring(6).Trim();
                
                if (string.IsNullOrWhiteSpace(rest)) continue;

                // Split to get encoder name (first word after flags)
                string[] parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1) continue;

                string name = parts[0];
                string type = "?";

                // Check type flags (position 0 = V/A/S, position 1-5 = other flags)
                // V = Video, A = Audio, S = Subtitle
                char typeChar = typeFlags[0];
                
                if (typeChar == 'V')
                {
                    encoders.Add((name, "V"));
                    type = "V";
                    parsedCount++;
                }
                else if (typeChar == 'A')
                {
                    encoders.Add((name, "A"));
                    type = "A";
                    parsedCount++;
                }
                else if (typeChar == 'S')
                {
                    encoders.Add((name, "S"));
                    type = "S";
                    parsedCount++;
                }
                
                if (type != "?")
                {
                    Console.WriteLine($"[GetEncoders] Found {type} encoder: {name}");
                }
            }
            
            Console.WriteLine($"[GetEncoders] Parsed {parsedCount} encoders, total: {encoders.Count}");
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred
            Console.WriteLine($"[GetEncoders] Timeout");
            return encoders;
        }
        catch (Exception ex)
        {
            // Log and return partial results
            Console.WriteLine($"[GetEncoders] Error: {ex.Message}");
            return encoders;
        }

        return encoders;
    }

    private async Task<List<string>> GetContainers()
    {
        List<string> containers = new();
        try
        {
            Console.WriteLine($"[GetContainers] Called");
            string output = await RunFfmpeg("-formats");
            Console.WriteLine($"[GetContainers] Received {output.Length} characters");
            
            if (string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"[GetContainers] Output is empty!");
                return containers;
            }

            // Split by newline and clean up lines
            string[] lines = output.Split('\n');
            Console.WriteLine($"[GetContainers] Split into {lines.Length} lines");
            
            bool inFormats = false;
            int parsedCount = 0;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim('\r');  // Remove carriage returns
                
                // Skip until we find the separator line (dashes)
                if (line.Trim() == "---")
                {
                    inFormats = true;
                    Console.WriteLine($"[GetContainers] Found separator line");
                    continue;
                }

                if (!inFormats) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Format from ffmpeg -formats:
                // " D   3dostr          3DO STR"
                // " .E. 3g2             3GP2"
                // "DE   ac3             raw AC-3"
                // Positions: D (demux), E (mux/encode), d (device)
                
                // Skip lines that don't start with space
                if (!line.StartsWith(" ")) continue;

                // Check if line has E flag (muxing/encoder support)
                // The E flag can be at position 1 or 2 (0-based: after initial space)
                if (line.Length < 4) continue;
                
                // Look for E flag in the format section (first 3-4 characters)
                bool hasEncoderFlag = false;
                if (line.Length > 1 && line[1] == 'E') hasEncoderFlag = true;  // Position 1
                if (line.Length > 2 && line[2] == 'E') hasEncoderFlag = true;  // Position 2

                if (!hasEncoderFlag) continue;

                // Extract container name (after the flags, starting from position 4)
                string rest = line.Substring(4).Trim();
                
                if (string.IsNullOrWhiteSpace(rest)) continue;

                // Split to get container name (first word)
                string[] parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1) continue;

                string name = parts[0];
                containers.Add(name);
                parsedCount++;
                Console.WriteLine($"[GetContainers] Found container: {name}");
            }
            
            Console.WriteLine($"[GetContainers] Parsed {parsedCount} containers, total: {containers.Count}");
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred
            Console.WriteLine($"[GetContainers] Timeout");
            return containers;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetContainers] Error: {ex.Message}");
            return containers;
        }

        return containers;
    }

    private async Task<Dictionary<string, EncoderOption>> GetEncoderOptions(string encoderName)
    {
        Dictionary<string, EncoderOption> options = new();
        string output = await RunFfmpeg($"-h encoder={encoderName}");

        string[] lines = output.Split('\n');
        bool inOptions = false;

        foreach (string line in lines)
        {
            if (line.Contains("Encoder")) inOptions = true;
            if (inOptions && line.StartsWith("-"))
            {
                Match match = Regex.Match(line, @"-(\w+)\s+<(.+?)>\s+(.*)");
                if (match.Success)
                {
                    string name = match.Groups[1].Value;
                    string type = match.Groups[2].Value;
                    string help = match.Groups[3].Value;

                    options[name] = new()
                    {
                        Name = name,
                        Type = type,
                        Help = help
                    };
                }
            }
        }

        return options;
    }

    private async Task<string> RunFfmpeg(string args)
    {
        // Use Shell.ExecAsync which properly handles both stdout and stderr without deadlock
        // using event-based reading instead of ReadToEndAsync
        Shell.ExecOptions options = new()
        {
            CaptureStdOut = true,
            CaptureStdErr = true,
            MergeStdErrToOut = false,  // Keep separate initially
            CreateNoWindow = true
        };

        Console.WriteLine($"[CapabilityGenerator] Running FFmpeg: {_ffmpegPath} {args}");

        Shell.ExecResult result = await NoMercy.NmSystem.SystemCalls.Shell.ExecAsync(_ffmpegPath, args, options);

        Console.WriteLine($"[CapabilityGenerator] Process exited with code {result.ExitCode}");
        Console.WriteLine($"[CapabilityGenerator] Stdout length: {result.StandardOutput.Length} characters");
        Console.WriteLine($"[CapabilityGenerator] Stderr length: {result.StandardError.Length} characters");

        // FFmpeg writes capability lists to stdout, not stderr
        // Stderr contains version/banner info
        string output = result.StandardOutput;
        if (string.IsNullOrEmpty(output))
        {
            // Fallback to stderr if stdout is empty
            output = result.StandardError;
            if (string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"[CapabilityGenerator] WARNING: Empty output from FFmpeg!");
                return string.Empty;
            }
        }

        Console.WriteLine($"[CapabilityGenerator] Using output length: {output.Length} characters");
        return output;
    }
}
