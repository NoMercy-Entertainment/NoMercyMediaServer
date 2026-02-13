using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NoMercy.Tray.Models;
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

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.C
            && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            System.Collections.IList selectedItems =
                LogList.SelectedItems;

            if (selectedItems.Count == 0)
                return;

            StringBuilder sb = new();

            foreach (object? item in selectedItems)
            {
                if (item is not LogEntryResponse entry)
                    continue;

                sb.AppendLine(
                    $"{entry.Time:HH:mm:ss.fff}\t{entry.Level}\t{entry.Type}\t{entry.Message}");
            }

            if (sb.Length > 0 && Clipboard is not null)
            {
                await Clipboard.SetTextAsync(sb.ToString());
                e.Handled = true;
            }
        }
    }
}
