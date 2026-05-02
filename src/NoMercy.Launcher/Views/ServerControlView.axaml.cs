using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Launcher.Services;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

public partial class ServerControlView : UserControl
{
    public ServerControlView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ServerControlViewModel? ViewModel => DataContext as ServerControlViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.ShowActiveSessionDialog = async activity =>
        {
            Window? owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null)
                return false;

            return await ActiveSessionDialog.ShowAsync(owner, activity);
        };
    }

    /// <summary>
    /// Wrap an async-void click handler so an exception from the awaited
    /// view-model task lands in LauncherLog instead of crashing the
    /// launcher via the AppDomain unhandled-exception path.
    /// </summary>
    private static async void SafeRun(string label, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"ServerControlView.{label} failed: {ex.Message}", ex);
        }
    }

    private void OnOpenAppClick(object? sender, RoutedEventArgs e) =>
        SafeRun(nameof(OnOpenAppClick), () => ViewModel?.LaunchAppAsync() ?? Task.CompletedTask);

    private void OnStartClick(object? sender, RoutedEventArgs e) =>
        SafeRun(nameof(OnStartClick), () => ViewModel?.StartServerAsync() ?? Task.CompletedTask);

    private void OnStopClick(object? sender, RoutedEventArgs e) =>
        SafeRun(nameof(OnStopClick), () => ViewModel?.StopServerAsync() ?? Task.CompletedTask);

    private void OnRestartClick(object? sender, RoutedEventArgs e) =>
        SafeRun(
            nameof(OnRestartClick),
            () => ViewModel?.RestartServerAsync() ?? Task.CompletedTask
        );

    private void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        SafeRun(
            nameof(OnRefreshClick),
            () => ViewModel?.RefreshStatusAsync() ?? Task.CompletedTask
        );

    private void OnApplyUpdate(object? sender, RoutedEventArgs e) =>
        SafeRun(nameof(OnApplyUpdate), () => ViewModel?.ApplyUpdateAsync() ?? Task.CompletedTask);

    private void OnAutoStartToggle(object? sender, RoutedEventArgs e) =>
        SafeRun(
            nameof(OnAutoStartToggle),
            () =>
                ViewModel is not null && sender is CheckBox checkBox
                    ? ViewModel.ToggleAutoStartAsync(checkBox.IsChecked == true)
                    : Task.CompletedTask
        );
}
