using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoMercy.Tray.Services;

namespace NoMercy.Tray.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private int _selectedTabIndex;

    public ServerControlViewModel ServerControlViewModel { get; }
    public LogViewerViewModel LogViewerViewModel { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public MainViewModel(
        ServerConnection serverConnection,
        ServerProcessLauncher processLauncher)
    {
        ServerControlViewModel = new(serverConnection, processLauncher);
        LogViewerViewModel = new(serverConnection);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this, new(propertyName));
    }
}
