# Encoder Building-Block Plugins

The NoMercy encoder exposes a set of swappable building-block interfaces.
Any host application or plugin assembly can replace the default implementation
by registering a new binding **after** calling `AddNoMercyEncoder()`.
Microsoft's DI container returns the last registration for
`GetRequiredService<T>`, so no special hook or decorator is required.

## Replaceable building blocks

- `IFontExtractor` — extracts embedded fonts from mkv/mp4 containers
- `ISubtitleExtractor` — pulls subtitle tracks to disk as srt/ass/vtt
- `IChapterWriter` — writes chapter metadata into the output container
- `IThumbnailGenerator` — generates sprite sheets and poster thumbnails
- `IPlaylistGenerator` — builds HLS master/variant playlists
- `IFilterGraphBuilder` — assembles FFmpeg `-filter_complex` graphs
- `IHlsVariantAnalyzer` — inspects finished HLS variants for quality checks
- `IAbrLadderGenerator` — decides which resolution tiers to encode
- `INotificationDispatcher` — delivers webhook/push notifications on job events
- `IWorkerDispatcher` — routes encode tasks to local or remote workers

## Registering a replacement

```csharp
// In your plugin's IPluginServiceRegistrator.RegisterServices():
public void RegisterServices(IServiceCollection services)
{
    // Override the default FontExtractor with your own implementation.
    services.AddTransient<IFontExtractor, MyCustomFontExtractor>();
}
```

Call `services.RegisterPluginServices(pluginManager)` after
`services.AddNoMercyEncoder(...)` so plugin registrations land last and win.

## Lifetime rules

Building blocks are registered as **Transient** by default — a new instance
is created per resolution. Replacement implementations must be safe to
instantiate multiple times concurrently. If your implementation holds shared
state (e.g. a connection pool), register it as **Singleton** explicitly; the
container will honour whichever lifetime you declare on the replacement.
Do not capture scoped services in a Singleton replacement.
