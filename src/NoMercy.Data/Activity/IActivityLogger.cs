// NoMercy.Data.Activity.IActivityLogger is an alias for the canonical interface
// defined in NoMercy.Database.Activity. Callers within NoMercy.Data and NoMercy.Api
// can continue using `using NoMercy.Data.Activity;` — the type is the same object.
namespace NoMercy.Data.Activity;

public interface IActivityLogger : NoMercy.Database.Activity.IActivityLogger { }
