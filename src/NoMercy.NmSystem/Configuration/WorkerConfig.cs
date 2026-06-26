namespace NoMercy.NmSystem.Configuration;

public class WorkerConfig
{
    public int LibraryWorkers { get; set; } = 1;
    public int ImportWorkers { get; set; } = 2;
    public int ExtrasWorkers { get; set; } = 4;
    public int EncoderWorkers { get; set; } = 1;
    public int EncoderTaskWorkers { get; set; } = 0;
    public int GpuEncoderWorkers { get; set; } = 1;
    public int CpuEncoderWorkers { get; set; } = 1;
    public int CronWorkers { get; set; } = 1;
    public int ImageWorkers { get; set; } = 3;
    public int FileWorkers { get; set; } = 2;
    public int MusicWorkers { get; set; } = 2;
    public int PaletteWorkers { get; set; } = 1;
}
