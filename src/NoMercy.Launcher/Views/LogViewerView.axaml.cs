using System.Collections.Specialized;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NoMercy.Launcher.Models;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

public partial class LogViewerView : UserControl
{
    public LogViewerView()
    {
        InitializeComponent();
    }

    private LogViewerViewModel? ViewModel =>
        DataContext as LogViewerViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (ViewModel is not null)
            ViewModel.FilteredEntries.CollectionChanged += (_, _) => ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (LogList.ItemCount > 0)
                LogList.ScrollIntoView(LogList.Items[LogList.ItemCount - 1]!);
        });
    }

    private async void OnRefreshClick(
        object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.RefreshLogsAsync();
    }

    private void OnClearFiltersClick(
        object? sender, RoutedEventArgs e)
    {
        ViewModel?.ClearFilters();
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.C
            && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            System.Collections.IList? selectedItems =
                LogList.SelectedItems;

            if (selectedItems is null || selectedItems.Count == 0)
                return;

            StringBuilder sb = new();

            foreach (object? item in selectedItems)
            {
                if (item is not LogEntryResponse entry)
                    continue;

                sb.AppendLine(
                    $"{entry.Time:HH:mm:ss.fff}\t{entry.Level}\t{entry.Type}\t{entry.Message}");
            }

            if (sb.Length > 0)
            {
                TopLevel? topLevel = TopLevel.GetTopLevel(this);

                if (topLevel?.Clipboard is not null)
                {
                    await topLevel.Clipboard.SetTextAsync(
                        sb.ToString());
                    e.Handled = true;
                }
            }
        }
    }
}
