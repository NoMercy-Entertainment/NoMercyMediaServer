using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.EncoderV2.Tasks;

public partial class DetectFrameCrop: ITaskContract
{
    private string InputFile { get; set; }
    private double Duration { get; set; }
    private ConcurrentDictionary<string, int> _counts = new();
    
    public DetectFrameCrop(string inputFile, double duration = 0)
    {
        InputFile = inputFile;
        Duration = duration;
    }

    public async Task Run(CancellationTokenSource cts)
    {
        Logger.Encoder($"Detecting crop for {InputFile}");

        const int sections = 10;

        double max = Math.Floor(Duration / 2);
        double step = Math.Floor(max / sections);

        ConcurrentDictionary<string, int> counts = new();
        Regex regex = new(@"crop=(\d+:\d+:\d+:\d+)", RegexOptions.Compiled);

        //List<string> results = [];
        ConcurrentBag<string> results = new();

        Parallel.For(0, sections, Config.ParallelOptions, (i, _) =>
        {
            string cropSection =
                $"-threads 1 -nostats -hide_banner -ss {i * step} -i \"{InputFile}\" -vframes 10 -vf cropdetect -t {1} -f null -";

            string result = Shell.ExecStdErrSync(AppFiles.FfmpegPath, cropSection, cts: cts);
            results.Add(result);
        });

        Parallel.ForEach(results, Config.ParallelOptions, (output) =>
        {
            MatchCollection matches = regex.Matches(output);

            foreach (Match match in matches)
            {
                string crop = match.Groups[1].Value;
                if (!counts.TryAdd(crop, 1)) counts[crop]++;
            }
        });

        _counts = counts;
    }
    
    public string Get()
    {
        return ChooseCrop(_counts);
    }
    
    public static async Task<string> GetStatic(string inputFile, CancellationTokenSource cts)
    {
        DetectFrameCrop cropDetect = new(inputFile);
        await cropDetect.Run(cts);
        
        return cropDetect.ChooseCrop(cropDetect._counts);
    }

    private string ChooseCrop(ConcurrentDictionary<string, int> crops)
    {
        string maxKey = "";
        int maxValue = 0;

        foreach (KeyValuePair<string, int> crop in crops)
        {
            if (crop.Value <= maxValue) continue;
            
            maxValue = crop.Value;
            maxKey = crop.Key;
        }

        return maxKey;
    }
    
}