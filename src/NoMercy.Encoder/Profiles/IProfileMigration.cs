namespace NoMercy.Encoder.Profiles;

using Newtonsoft.Json.Linq;

public interface IProfileMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    JObject Migrate(JObject input);
}
