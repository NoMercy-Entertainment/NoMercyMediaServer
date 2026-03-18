using NoMercy.NmSystem.SystemCalls;
using NoMercyQueue.Core.Interfaces;
using Serilog.Events;

namespace NoMercy.Data.Jobs;

[Serializable]
public class MusicColorPaletteJob : IShouldQueue
{
    public string QueueName => "image";
    public int Priority => 1;

    public string? Id { get; set; }
    public string? Model { get; set; }
    public string? FilePath { get; set; }
    public bool? Download { get; set; }

    public MusicColorPaletteJob()
    {
        //
    }

    public MusicColorPaletteJob(string id, string model)
    {
        Id = id;
        Model = model;
    }

    public MusicColorPaletteJob(string filePath, string model, bool? download = false)
    {
        FilePath = filePath;
        Model = model;
        Download = download;
    }

    public async Task Handle()
    {
        switch (Model)
        {
            case "image" when FilePath == null:
                await Task.CompletedTask;
                return;
            case "image":
                // await ImageLogic2.MusicPalette(FilePath, Download ?? false);
                break;
            // case "fanart":
            //     await MusicLogic2.Palette(Id!, Model);
            //     break;
            // case "artist":
            //     await MusicLogic2.Palette(Id!, Model);
            //     break;
            // case "album":
            //     await MusicLogic2.Palette(Id!, Model);
            //     break;
            // case "track":
            //     await MusicLogic2.Palette(Id!, Model);
            //     break;
            default:
                Logger.Queue(@"Invalid model Type: " + Model + @" id: " + Id, LogEventLevel.Error);
                break;
        }
    }
}