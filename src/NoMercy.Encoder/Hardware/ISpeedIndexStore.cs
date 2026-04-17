namespace NoMercy.Encoder.Hardware;

public interface ISpeedIndexStore
{
    SpeedIndex? Load();
    void Save(SpeedIndex index);
    DateTime? LastCalibratedAt { get; }
}
