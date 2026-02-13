using Avalonia.Controls;
using NoMercy.Tray.ViewModels;

namespace NoMercy.Tray.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
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
        await _viewModel.ServerControlViewModel.RefreshStatusAsync();
        _viewModel.ServerControlViewModel.StartPolling();

        await _viewModel.LogViewerViewModel.RefreshLogsAsync();

        if (_viewModel.LogViewerViewModel.AutoRefresh)
            _viewModel.LogViewerViewModel.StartAutoRefresh();
    }

    private void OnWindowClosing(
        object? sender, WindowClosingEventArgs e)
    {
        _viewModel.ServerControlViewModel.StopPolling();
        _viewModel.LogViewerViewModel.StopAutoRefresh();
    }

    public void SelectTab(int index)
    {
        _viewModel.SelectedTabIndex = index;
    }
}
