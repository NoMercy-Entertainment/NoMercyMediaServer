using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NoMercy.Launcher.Services;

namespace NoMercy.Launcher;

public class App : Application
{
    private TrayIconManager? _trayIconManager;
    private ServerConnection? _serverConnection;
    private ServerProcessLauncher? _processLauncher;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serverConnection = new();
        _processLauncher = new();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _trayIconManager = new(
                _serverConnection, _processLauncher, desktop,
                Program.ShowOnStartup, Program.IsDev);
            _trayIconManager.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
