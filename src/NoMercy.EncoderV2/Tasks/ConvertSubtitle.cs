using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NoMercy.Encoder.Core;
using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.EncoderV2.Core.Dictionaries;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using TesseractLanguageDownloader = NoMercy.EncoderV2.Core.TesseractLanguageDownloader;

namespace NoMercy.EncoderV2.Tasks;

public partial class ConvertSubtitle: ITaskContract
{
    private string InputFile { get; }
    private string Destination { get; }
    private string SubtitlesFolder { get; }
    private string FileName { get; }
    private string Language { get; }
    private string Type { get; }
    private string Extension => "vtt";
    private string SubtitleFileName => $"{FileName}.{Language}.{Type}.{Extension}";
    
    public ConvertSubtitle(string inputFile, string destination, string fileName, string language, string type)
    {
        InputFile = inputFile;
        Destination = destination;
        FileName = fileName;
        Language = language;
        Type = type;
        
        SubtitlesFolder = Path.Combine(Destination, "subtitles");
    }
    
    private string GetCommand()
    {
        // return $" -i \"{InputFile}\" -f lavfi -i color=black:s=hd720 -filter_complex \"[0:s:0]ocr=language={Language},metadata=print:key=lavfi.ocr.text:file=temp.txt\" -an -f null -";
        StringBuilder ocrCommand = new();
        ocrCommand.Append($"-i \"{InputFile}\" ");
        ocrCommand.Append("-f lavfi -i color=black:s=hd720 ");
        ocrCommand.Append($"-filter_complex \"[0:s:0]ocr=language={Language},metadata=print:key=lavfi.ocr.text:file=temp.txt\" ");
        ocrCommand.Append("-an -f null -");
        
        return ocrCommand.ToString();
    }

    public async Task Run(CancellationTokenSource cts)
    {
        bool languageFileExists = await TesseractLanguageDownloader.EnsureLanguageFileExists(Language);
        if (!languageFileExists)
        {
            throw new($"Failed to obtain Tesseract language file for {Language}. Skipping OCR for this subtitle.");
        }

        string ocrCommand = GetCommand();

        Logger.Encoder($"Converting {IsoLanguageDictionary.GetLanguageFromIso6392(Language)} subtitle to WebVtt");
        Logger.Encoder(AppFiles.FfmpegPath + " " + ocrCommand, LogEventLevel.Debug);

        if (!Directory.Exists(SubtitlesFolder))
            Directory.CreateDirectory(SubtitlesFolder);
        
        await Shell.ExecAsync(AppFiles.FfmpegPath, ocrCommand, new()
        {
            WorkingDirectory = SubtitlesFolder,
            EnvironmentVariables = new()
            {
                ["TESSDATA_PREFIX"] = AppFiles.TesseractModelsFolder
            }
        }, cts);
        
        string orcFile = Path.Combine(SubtitlesFolder, "temp.txt");
        string output = Path.Combine(Destination, SubtitleFileName);
        
        if (!File.Exists(orcFile)) return;

        Subtitle[] parsedSubtitles = ParseSubtitles(orcFile);

        SaveToVtt(parsedSubtitles, output);

        File.Delete(orcFile);
    }
    
    public static async Task RunStatic(string inputFile, string destination, string fileName, string language, string type, CancellationTokenSource cts)
    {
        ConvertSubtitle task = new(inputFile, destination, fileName, language, type);
        await task.Run(cts);
    }
    
    private static Subtitle[] ParseSubtitles(string filePath)
    {
        List<Subtitle> subtitles = [];
        string fileContent = File.ReadAllText(filePath);

        MatchCollection matches = new Regex(
                @"pts_time:(?<start>\d+(\.\d+)?)\nlavfi\.ocr\.text=(?<text>.+(\n.+)?)\n\n.*?pts_time:(?<end>\d+(\.\d+)?)",
                RegexOptions.Multiline).Matches(fileContent);

        foreach (Match match in matches)
        {
            double startTime = double.Parse(match.Groups["start"].Value, CultureInfo.InvariantCulture);
            double endTime = double.Parse(match.Groups["end"].Value, CultureInfo.InvariantCulture);
            string text = match.Groups["text"].Value.Trim();

            subtitles.Add(new(startTime, endTime, text));
        }

        return subtitles.ToArray();
    }

    private static void SaveToVtt(Subtitle[] subtitles, string filePath)
    {
        using StreamWriter writer = new(filePath);
        writer.WriteLine("WEBVTT");
        writer.WriteLine();

        foreach (Subtitle subtitle in subtitles)
        {
            writer.WriteLine($"{FormatTime(subtitle.StartTime)} --> {FormatTime(subtitle.EndTime)}");
            writer.WriteLine(subtitle.Text);
            writer.WriteLine();
        }
    }

    private static string FormatTime(double timeInSeconds)
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(timeInSeconds);
        return string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
            timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
    }
}