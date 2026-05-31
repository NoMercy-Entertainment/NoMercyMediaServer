using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Launcher.Models;

namespace NoMercy.Launcher.Views;

public partial class ActiveSessionDialog : Window
{
    private bool _interrupted;

    public ActiveSessionDialog()
    {
        InitializeComponent();
    }

    public static async Task<bool> ShowAsync(Window owner, ActivityInfo activity)
    {
        ActiveSessionDialog dialog = new();
        dialog.Configure(activity);
        await dialog.ShowDialog(owner);
        return dialog._interrupted;
    }

    private void Configure(ActivityInfo activity)
    {
        List<string> parts = [];

        if (activity.ActiveStreams > 0)
            parts.Add(
                $"{activity.ActiveStreams} active stream{(activity.ActiveStreams == 1 ? "" : "s")} — interrupting will stop playback for those users"
            );

        if (activity.ActiveEncodes > 0)
            parts.Add(
                $"{activity.ActiveEncodes} active encode{(activity.ActiveEncodes == 1 ? "" : "s")} — these will resume where they left off"
            );

        ActivitySummary.Text = string.Join("\n", parts);

        ResumeNote.Text =
            activity.ActiveEncodes > 0
                ? "Active encodes will resume where they left off."
                : string.Empty;
    }

    private void OnWaitClick(object? sender, RoutedEventArgs e)
    {
        _interrupted = false;
        Close();
    }

    private void OnInterruptClick(object? sender, RoutedEventArgs e)
    {
        _interrupted = true;
        Close();
    }
}
