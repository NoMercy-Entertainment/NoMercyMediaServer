using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Launcher.Services;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

public partial class StartupArgumentsView : UserControl
{
    public StartupArgumentsView()
    {
        InitializeComponent();
    }

    private StartupArgumentsViewModel? ViewModel => DataContext as StartupArgumentsViewModel;

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is not null)
                await ViewModel.SaveAsync();
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"StartupArgumentsView.OnSaveClick failed: {ex.Message}", ex);
        }
    }
}
