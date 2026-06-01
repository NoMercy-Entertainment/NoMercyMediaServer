using Avalonia.Controls;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        InitializeComponent();

        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_viewModel is null)
            return;

        await _viewModel.ServerControlViewModel.RefreshStatusAsync();
        _viewModel.ServerControlViewModel.StartPolling();

        if (_viewModel.ServerControlViewModel.IsServerRunning)
            await _viewModel.SettingsViewModel.LoadConfigAsync();

        await _viewModel.StartupArgumentsViewModel.LoadAsync();

        if (_viewModel.LogViewerViewModel.AutoRefresh)
            _viewModel.LogViewerViewModel.StartAutoRefresh();
        else
            await _viewModel.LogViewerViewModel.RefreshLogsAsync();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _viewModel?.ServerControlViewModel.StopPolling();
        _viewModel?.LogViewerViewModel.StopAutoRefresh();
    }

    public void SelectTab(int index)
    {
        if (_viewModel is not null)
            _viewModel.SelectedTabIndex = index;
    }
}
