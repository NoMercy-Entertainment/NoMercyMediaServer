using Avalonia.Controls;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

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

        await _viewModel.StartupArgumentsViewModel.LoadAsync();

        if (_viewModel.LogViewerViewModel.AutoRefresh)
            _viewModel.LogViewerViewModel.StartAutoRefresh();
        else
            await _viewModel.LogViewerViewModel.RefreshLogsAsync();
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
