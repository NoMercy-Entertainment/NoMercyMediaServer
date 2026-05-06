namespace NoMercy.Encoder.Profiles;

public interface IPresetLookup
{
    (string ProfileJson, Ulid? ParentPresetId)? Get(Ulid presetId);
}
