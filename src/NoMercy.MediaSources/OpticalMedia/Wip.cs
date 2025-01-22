using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDInfo;
using BDInfo.IO;
using NoMercy.Encoder;
using NoMercy.Encoder.Core;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using DirectoryInfo = BDInfo.IO.DirectoryInfo;

namespace NoMercy.MediaSources.OpticalMedia;

public class Wip
{
    public static Task Run()
    {
        // string fileName = "E:/";
        // string fileName = @"M:\Anime\Download\Bleach\[BDMV] Bleach [BD-BOX] [SET-1]\BLEACH SET 1 DISC 2";
        IDirectoryInfo directoryInfo = new DirectoryInfo(@"H:\TV.Shows\Download\The.Pink.Panther\The Pink Panther - La Pantera Rosa Vol 2 (1966-1968) [Bluray 1080p AVC Eng DTS-HD MA 2.0]");
        string ffmpegExecutable = @"H:\C\Downloads\ffmpeg-build-windows\ffmpeg.exe";
        string ffprobeExecutable = @"H:\C\Downloads\ffmpeg-build-windows\ffprobe.exe";
        
        string metadataFile = Path.Combine(directoryInfo.FullName, "BDMV", "META", "DL", "bdmt_eng.xml");

        string xmlContent = File.ReadAllText(metadataFile);

        BDROM bDRom = new(directoryInfo);
        try
        {
            bDRom.Scan();
        }
        catch (Exception e)
        {
            //
        }

        XDocument doc = XDocument.Parse(xmlContent);
        XNamespace ns = "urn:BDA:bdmv;disclib";
        XNamespace di = "urn:BDA:bdmv;discinfo";

        string title = doc.Descendants(di + "name").FirstOrDefault()?.Value ?? bDRom.VolumeLabel;

        string playlistString = FfMpeg
            .Exec($" -hide_banner -v info -i \"bluray:{directoryInfo.FullName}\"", executable: ffprobeExecutable).Result;

        string ffprobeString = HlsPlaylistGenerator.RunProcess(AppFiles.FfProbePath,
            $" -v quiet -show_programs -show_format -show_streams -show_data -show_chapters -sexagesimal -print_format json \"bluray:{directoryInfo.FullName}\"");

        File.WriteAllText(Path.Combine(AppFiles.TempPath, "bdrom.json"), bDRom.ToJson());
        File.WriteAllText(Path.Combine(AppFiles.TempPath, "analysis.json"), ffprobeString);

        // string playlistString = HlsPlaylistGenerator.RunProcess(AppFiles.FfmpegPath,
        //     $" -v info \"bluray:{fileName}\"");

        Regex regex = new(@"\[bluray.*?playlist\s(?<playlist>\d+).mpls\s\((?<duration>\d{1,}:\d{1,}:\d{1,})\)");
        List<Match> matches = regex.Matches(playlistString).ToList();

        foreach (Match match in matches)
        {
            using MemoryStream memoryStream = new();
            StringBuilder sb = new();

            int matchIndex = matches.IndexOf(match);

            string matchTitle = $"{title} {matchIndex + 1}".Replace(":", "");
            string outputFile = Path.Combine(@"G:\TV.Shows\Download\The.Pink.Panther", $"{matchTitle}.mkv");
            string chaptersFile = Path.Combine(AppFiles.TempPath, $"{matchTitle}.txt");

            string playlist = match.Groups["playlist"].Value;

            TSPlaylistFile playlistFile = bDRom.PlaylistFiles.FirstOrDefault(c => c.Key.StartsWith(playlist)).Value;
            List<TSStream> streams = playlistFile.PlaylistStreams.Values.ToList();
            int duration = playlistFile.TotalLength.ToInt();
            List<double>? chapters = playlistFile.Chapters;

            // if (duration != playlistFile.TotalLength.ToInt())
            // {
            //     continue;
            // }

            Logger.Encoder(duration.ToString());
            Logger.Encoder(chapters);

            sb.Append(" -hide_banner -v info ");

            sb.Append($" -playlist {playlist} -i \"bluray:{directoryInfo.FullName}\"");

            StringBuilder chapterSb = new();
            chapterSb.AppendLine(";FFMETADATA1");
            chapterSb.AppendLine($"title={matchTitle}");
            chapterSb.AppendLine("");

            foreach (double start in chapters)
            {
                int chapterIndex = chapters.IndexOf(start);
                double end = chapterIndex < chapters.Count - 1 ? chapters[chapterIndex + 1] : duration;

                chapterSb.AppendLine("[CHAPTER]");
                chapterSb.AppendLine("TIMEBASE=1/1000");
                chapterSb.AppendLine($"START={(int)start * 1000}");
                chapterSb.AppendLine($"END={(int)end * 1000 - 1}");
                chapterSb.AppendLine($"title=Chapter {chapterIndex + 1}");
                chapterSb.AppendLine("");
            }

            File.WriteAllText(chaptersFile, chapterSb.ToString());

            sb.Append($" -i \"{chaptersFile}\" -map_metadata 1");

            sb.Append(" -c copy");

            foreach (TSStream stream in streams.Where(s => s.CodecShortName != "IGS"))
            {
                int index = streams.IndexOf(stream);
                string languageCode = stream.LanguageCode;
                string language = stream.LanguageName;

                sb.Append(
                    $" -map 0:{index} -metadata:s:{index} language={languageCode} -metadata:s:{index} title=\"{language ?? matchTitle}\"");
            }

            sb.Append($" -f matroska \"{outputFile}\" -y");

            string command = sb.ToString();

            Logger.Encoder(command + "\"");

            FfMpeg.Exec(command, executable: @"H:\C\Downloads\ffmpeg-build-windows\ffmpeg.exe").Wait();
            // Logger.Encoder();

        }
        
        return Task.CompletedTask;
    }
}