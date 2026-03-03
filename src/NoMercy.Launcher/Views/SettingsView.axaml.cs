using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? ViewModel =>
        DataContext as SettingsViewModel;

    private async void OnSaveConfigClick(
        object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.SaveConfigAsync();
    }
}
