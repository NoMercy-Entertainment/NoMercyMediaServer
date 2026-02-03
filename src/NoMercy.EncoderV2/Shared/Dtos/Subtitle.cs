namespace NoMercy.EncoderV2.Shared.Dtos;

public class Subtitle
{
    public double StartTime { get; }
    public double EndTime { get; }
    public string Text { get; }

    public Subtitle(double startTime, double endTime, string text)
    {
        StartTime = startTime;
        EndTime = endTime;
        Text = text;
    }
}