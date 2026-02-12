using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Tray.ViewModels;

namespace NoMercy.Tray.Views;

public partial class ServerControlWindow : Window
{
    private readonly ServerControlViewModel _viewModel;

    public ServerControlWindow(ServerControlViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        InitializeComponent();

        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
    }

    private async void OnWindowOpened(
        object? sender, EventArgs e)
    {
        await _viewModel.RefreshStatusAsync();
        _viewModel.StartPolling();
    }

    private void OnWindowClosing(
        object? sender, WindowClosingEventArgs e)
    {
        _viewModel.StopPolling();
    }

    private async void OnStopClick(
        object? sender, RoutedEventArgs e)
    {
        await _viewModel.StopServerAsync();
    }

    private async void OnRestartClick(
        object? sender, RoutedEventArgs e)
    {
        await _viewModel.RestartServerAsync();
    }

    private async void OnRefreshClick(
        object? sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshStatusAsync();
    }
}
