using NoMercy.Helpers;
using NoMercy.Queue.system;
using NoMercy.Server.Logic;
using LogLevel = NoMercy.Helpers.LogLevel;

namespace NoMercy.Server.app.Jobs;

public class ColorPaletteJob : IShouldQueue
{
    private readonly int _id;
    private readonly string? _filePath;
    private readonly string? _type;
    private readonly string _model;

    public ColorPaletteJob(long id, string model, string type)
    {
        Logger.Queue($"Creating color palette job for {model} {id}");
        _id = (int)id;
        _model = model;
        _type = type;
    }
    
    public ColorPaletteJob(long id, string model)
    {
        Logger.Queue($"Creating color palette job for {model} {id}");
        _id = (int)id;
        _model = model;
    }

    public ColorPaletteJob(string filePath, string model)
    {
        _filePath = filePath;
        _model = model;
    }

    public async Task Handle()
    {
        switch (_model)
        {
            case "image" when _filePath == null:
                await Task.CompletedTask;
                return;
            case "image":
                await ImageLogic.GetPalette(_filePath);
                break;
            case "collection":
                await CollectionLogic.GetPalette(_id);
                break;
            case "tv":
                await TvShowLogic.GetPalette(_id);
                break;
            case "person":
                await PersonLogic.GetPalette(_id);
                break;
            case "season":
                await SeasonLogic.GetPalette(_id);
                break;
            case "episode":
                await EpisodeLogic.GetPalette(_id);
                break;
            case "movie":
                await MovieLogic.GetPalette(_id);
                break;
            case "recommendation" when _type == "movie":
                await MovieLogic.GetRecommendationPalette(_id);
                break;
            case "recommendation" when _type == "tv":
                await TvShowLogic.GetRecommendationPalette(_id);
                break;
            case "similar" when _type == "movie":
                await MovieLogic.GetSimilarPalette(_id);
                break;
            case "similar" when _type == "tv":
                await TvShowLogic.GetSimilarPalette(_id);
                break;
            default:
                Console.WriteLine(_model);
                Console.WriteLine(_filePath);
                Console.WriteLine(_id);
                Console.WriteLine(_type);
            
                Logger.Queue("Invalid model Type", LogLevel.Error);
                break;
        }
    }
}