using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Tray.ViewModels;

namespace NoMercy.Tray.Views;

public partial class LogViewerWindow : Window
{
    private readonly LogViewerViewModel _viewModel;

    public LogViewerWindow(LogViewerViewModel viewModel)
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
        await _viewModel.RefreshLogsAsync();

        if (_viewModel.AutoRefresh)
            _viewModel.StartAutoRefresh();
    }

    private void OnWindowClosing(
        object? sender, WindowClosingEventArgs e)
    {
        _viewModel.StopAutoRefresh();
    }

    private async void OnRefreshClick(
        object? sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshLogsAsync();
    }

    private void OnClearFiltersClick(
        object? sender, RoutedEventArgs e)
    {
        _viewModel.ClearFilters();
    }
}
