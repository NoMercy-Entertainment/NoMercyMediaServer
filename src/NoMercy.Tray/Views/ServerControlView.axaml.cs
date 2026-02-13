using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Tray.ViewModels;

namespace NoMercy.Tray.Views;

public partial class ServerControlView : UserControl
{
    public ServerControlView()
    {
        InitializeComponent();
    }

    private ServerControlViewModel? ViewModel =>
        DataContext as ServerControlViewModel;

    private async void OnStartClick(
        object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.StartServerAsync();
    }

    private async void OnStopClick(
        object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.StopServerAsync();
    }

    private async void OnRestartClick(
        object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.RestartServerAsync();
    }

    private async void OnRefreshClick(
        object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.RefreshStatusAsync();
    }

    private async void OnAutoStartToggle(
        object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && sender is CheckBox checkBox)
            await ViewModel.ToggleAutoStartAsync(
                checkBox.IsChecked == true);
    }
}
