using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Launcher.Models;
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

        ViewModel.ShowActiveSessionDialog = async (ActivityInfo activity) =>
        {
            Window? owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null)
                return false;

            return await ActiveSessionDialog.ShowAsync(owner, activity);
        };
    }

    private async void OnOpenAppClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.LaunchAppAsync();
    }

    private async void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.StartServerAsync();
    }

    private async void OnStopClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.StopServerAsync();
    }

    private async void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.RestartServerAsync();
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.RefreshStatusAsync();
    }

    private async void OnApplyUpdate(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.ApplyUpdateAsync();
    }

    private async void OnAutoStartToggle(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && sender is CheckBox checkBox)
            await ViewModel.ToggleAutoStartAsync(checkBox.IsChecked == true);
    }
}
