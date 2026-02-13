using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NoMercy.Tray.Services;

namespace NoMercy.Tray;

public class App : Application
{
    private TrayIconManager? _trayIconManager;
    private ServerConnection? _serverConnection;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serverConnection = new();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _trayIconManager = new(_serverConnection, desktop);
            _trayIconManager.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
