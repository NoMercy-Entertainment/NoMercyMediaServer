namespace NoMercy.Encoder.Profiles.V2;

using Newtonsoft.Json.Linq;

public interface IProfileMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    JObject Migrate(JObject input);
}
