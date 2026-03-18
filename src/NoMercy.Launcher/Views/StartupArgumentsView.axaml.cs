using Avalonia.Controls;
using Avalonia.Interactivity;
using NoMercy.Launcher.ViewModels;

namespace NoMercy.Launcher.Views;

public partial class StartupArgumentsView : UserControl
{
    public StartupArgumentsView()
    {
        InitializeComponent();
    }

    private StartupArgumentsViewModel? ViewModel =>
        DataContext as StartupArgumentsViewModel;

    private async void OnSaveClick(
        object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.SaveAsync();
    }
}
