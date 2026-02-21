using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;

namespace NoMercy.Launcher.ViewModels;

public class StartupArgumentsViewModel : INotifyPropertyChanged
{
    private string _startupArguments = string.Empty;
    private string _saveStatus = string.Empty;

    public string StartupArguments
    {
        get => _startupArguments;
        set { _startupArguments = value; OnPropertyChanged(); }
    }

    public string SaveStatus
    {
        get => _saveStatus;
        set { _saveStatus = value; OnPropertyChanged(); }
    }

    public Task LoadAsync()
    {
        TraySettings settings = LauncherSettings.Load();
        StartupArguments = settings.StartupArguments;
        return Task.CompletedTask;
    }

    public Task SaveAsync()
    {
        TraySettings settings = LauncherSettings.Load();
        settings.StartupArguments = StartupArguments;
        LauncherSettings.Save(settings);
        SaveStatus = "Saved";
        return Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this, new(propertyName));
    }
}
