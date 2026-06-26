// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.CompilerServices;
using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Launcher.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ServerConnection _serverConnection;

    public bool IsServerRunning
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool ConfigLoaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string ConfigServerName
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public int InternalPort
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int ExternalPort
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int LibraryWorkers
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int ImportWorkers
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int ExtrasWorkers
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int EncoderWorkers
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int CronWorkers
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int ImageWorkers
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int FileWorkers
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public int MusicWorkers
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string ActionStatus
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public SettingsViewModel(ServerConnection serverConnection)
    {
        _serverConnection = serverConnection;
    }

    public async Task LoadConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!_serverConnection.IsConnected)
            await _serverConnection.ConnectAsync(cancellationToken);

        ServerConfigResponse? config = await _serverConnection.GetAsync<ServerConfigResponse>(
            "/manage/config",
            cancellationToken
        );

        if (config is null)
            return;

        ConfigServerName = config.ServerName.OrEmpty();
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

    public async Task SaveConfigAsync(CancellationToken cancellationToken = default)
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
                    music_workers = MusicWorkers,
                },
                cancellationToken
            );

            ActionStatus = success ? "Configuration saved" : "Failed to save configuration";
        }
        catch
        {
            ActionStatus = "Failed to save configuration";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }
}
