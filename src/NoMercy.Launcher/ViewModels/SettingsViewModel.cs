using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;

namespace NoMercy.Launcher.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ServerConnection _serverConnection;

    private bool _isServerRunning;
    private bool _configLoaded;
    private string _configServerName = string.Empty;
    private int _internalPort;
    private int _externalPort;
    private int _libraryWorkers;
    private int _importWorkers;
    private int _extrasWorkers;
    private int _encoderWorkers;
    private int _cronWorkers;
    private int _imageWorkers;
    private int _fileWorkers;
    private int _musicWorkers;
    private string _actionStatus = string.Empty;

    public bool IsServerRunning
    {
        get => _isServerRunning;
        set { _isServerRunning = value; OnPropertyChanged(); }
    }

    public bool ConfigLoaded
    {
        get => _configLoaded;
        set { _configLoaded = value; OnPropertyChanged(); }
    }

    public string ConfigServerName
    {
        get => _configServerName;
        set { _configServerName = value; OnPropertyChanged(); }
    }

    public int InternalPort
    {
        get => _internalPort;
        set { _internalPort = value; OnPropertyChanged(); }
    }

    public int ExternalPort
    {
        get => _externalPort;
        set { _externalPort = value; OnPropertyChanged(); }
    }

    public int LibraryWorkers
    {
        get => _libraryWorkers;
        set { _libraryWorkers = value; OnPropertyChanged(); }
    }

    public int ImportWorkers
    {
        get => _importWorkers;
        set { _importWorkers = value; OnPropertyChanged(); }
    }

    public int ExtrasWorkers
    {
        get => _extrasWorkers;
        set { _extrasWorkers = value; OnPropertyChanged(); }
    }

    public int EncoderWorkers
    {
        get => _encoderWorkers;
        set { _encoderWorkers = value; OnPropertyChanged(); }
    }

    public int CronWorkers
    {
        get => _cronWorkers;
        set { _cronWorkers = value; OnPropertyChanged(); }
    }

    public int ImageWorkers
    {
        get => _imageWorkers;
        set { _imageWorkers = value; OnPropertyChanged(); }
    }

    public int FileWorkers
    {
        get => _fileWorkers;
        set { _fileWorkers = value; OnPropertyChanged(); }
    }

    public int MusicWorkers
    {
        get => _musicWorkers;
        set { _musicWorkers = value; OnPropertyChanged(); }
    }

    public string ActionStatus
    {
        get => _actionStatus;
        set { _actionStatus = value; OnPropertyChanged(); }
    }

    public SettingsViewModel(ServerConnection serverConnection)
    {
        _serverConnection = serverConnection;
    }

    public async Task LoadConfigAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_serverConnection.IsConnected)
            await _serverConnection.ConnectAsync(cancellationToken);

        ServerConfigResponse? config =
            await _serverConnection.GetAsync<ServerConfigResponse>(
                "/manage/config", cancellationToken);

        if (config is null) return;

        ConfigServerName = config.ServerName ?? string.Empty;
        InternalPort = config.InternalPort;
        ExternalPort = config.ExternalPort;
        LibraryWorkers = config.LibraryWorkers;
        ImportWorkers = config.ImportWorkers;
        ExtrasWorkers = config.ExtrasWorkers;
        EncoderWorkers = config.EncoderWorkers;
        CronWorkers = config.CronWorkers;
        ImageWorkers = config.ImageWorkers;
        FileWorkers = config.FileWorkers;
        MusicWorkers = config.MusicWorkers;
        ConfigLoaded = true;
    }

    public async Task SaveConfigAsync(
        CancellationToken cancellationToken = default)
    {
        ActionStatus = "Saving configuration...";

        try
        {
            bool success = await _serverConnection.PutAsync(
                "/manage/config",
                new
                {
                    server_name = ConfigServerName,
                    library_workers = LibraryWorkers,
                    import_workers = ImportWorkers,
                    extras_workers = ExtrasWorkers,
                    encoder_workers = EncoderWorkers,
                    cron_workers = CronWorkers,
                    image_workers = ImageWorkers,
                    file_workers = FileWorkers,
                    music_workers = MusicWorkers
                },
                cancellationToken);

            ActionStatus = success
                ? "Configuration saved"
                : "Failed to save configuration";
        }
        catch
        {
            ActionStatus = "Failed to save configuration";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this, new(propertyName));
    }
}
