// using Discord;
// using Discord.WebSocket;
// using DiscordRPC;
// using DiscordRPC.Logging;
// //
// namespace NoMercy.Providers.Discord;
//
// public class DiscordBot
// {
//     private static readonly DiscordRpcClient DiscordRpcClient = new("952230846465728553");
//
//     public DiscordBot()
//     {
//         MainAsync().GetAwaiter().GetResult();
//     }
//
//     private Task MainAsync()
//     {
//         // Optional: Set the logger to see what's happening
//         DiscordRpcClient.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
//
//         // Subscribe to events (optional)
//         DiscordRpcClient.OnReady += (sender, e) =>
//         {
//             Logger.Discord($"Connected to Discord as {e.User.Username}");
//         };
//
//         DiscordRpcClient.OnError += (sender, e) =>
//         {
//             Logger.Discord($"Error: {e.Message}");
//         };
//
//         // Connect to Discord
//         DiscordRpcClient.Initialize();
//
//         UpdatePresence("Playing a Movie", "Inception", 3600);
//
//         Logger.Discord("Rich Presence updated!");
//
//         return Task.Delay(-1);
//     }
//
//     // Method to update Discord Rich Presence
//     private static void UpdatePresence(string details, string state, int duration)
//     {
//         DiscordRpcClient.SetPresence(new RichPresence()
//         {
//             Details = details,   // Main status (e.g., "Watching a Movie")
//             State = state,       // Secondary status (e.g., the movie name)
//             Timestamps = Timestamps.FromTimeSpan(TimeSpan.FromSeconds(duration)), // Show how long the user has been watching
//             Assets = new Assets()
//             {
//                 LargeImageKey = "logo", // The large image key
//                 LargeImageText = "NoMercy Media Server", // The large image tooltip
//                 SmallImageKey = "logo", // The small image key
//                 SmallImageText = "NoMercy Media Server" // The small image tooltip
//             },
//             Buttons =
//             [
//                 new() { Label = "NoMercy Media Server", Url = "https://nomercy.tv" }
//             ],
//             Party = new Party()
//             {
//                 ID = "NoMercyMediaServer",
//                 Size = 1,
//                 Max = 1
//             },
//         });
//     }
//
//     // private Task SetStatusAsync()
//     // {
//     //     Game game = new("NoMercyMediaServer", ActivityType.Playing);
//     //     DiscordSocketClient?.SetActivityAsync(game);
//     //
//     //     Logger.Discord("Status set successfully!");
//     //     return Task.CompletedTask;
//     // }
//     //
//     // public static async Task SetPlayingStatus(string status)
//     // {
//     //     Game game = new(status, ActivityType.Playing);
//     //     await DiscordSocketClient.SetActivityAsync(game);
//     // }
//     //
//     // public static async Task SetWatchingStatus(string status)
//     // {
//     //     Game game = new(status, ActivityType.Watching);
//     //     await DiscordSocketClient.SetActivityAsync(game);
//     // }
//     //
//     // public static async Task SetListeningStatus(string status)
//     // {
//     //     Game game = new(status, ActivityType.Listening);
//     //     await DiscordSocketClient.SetActivityAsync(game);
//     // }
//     //
//     // public static async Task SetStreamingStatus(string status)
//     // {
//     //     Game game = new(status, ActivityType.Streaming);
//     //     await DiscordSocketClient.SetActivityAsync(game);
//     // }
//     //
//     // public static async Task SetCompetingStatus(string status)
//     // {
//     //     Game game = new(status, ActivityType.Competing);
//     //     await DiscordSocketClient.SetActivityAsync(game);
//     // }
//     //
//     // public static async Task ClearStatus()
//     // {
//     //     await DiscordSocketClient.SetActivityAsync(null);
//     // }
// }
