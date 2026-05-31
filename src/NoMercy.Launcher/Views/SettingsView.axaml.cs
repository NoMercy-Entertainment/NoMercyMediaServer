using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Launcher.Services;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    private async void OnSaveConfigClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is not null)
                await ViewModel.SaveConfigAsync();
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"SettingsView.OnSaveConfigClick failed: {ex.Message}", ex);
        }
    }
}
