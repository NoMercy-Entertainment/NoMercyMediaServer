namespace NoMercy.Encoder.Profiles.V2;

public interface IPresetLookup
{
    (string ProfileJson, Ulid? ParentPresetId)? Get(Ulid presetId);
}
