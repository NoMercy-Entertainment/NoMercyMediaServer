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

using System.Security;
using AniDB;
using AniDB.RequestEnums;
using AniDB.ResponseItems;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.AniDb.Models;
using Serilog.Events;

namespace NoMercy.Providers.AniDb.Client;

public class AniDbService : IAniDbService
{
    private string _username = "";
    private string _password = "";
    private SecureString? _apiKey;

    private readonly Barrier _disconnectBarrier = new(participantCount: 2);
    private readonly AniDBClient _client;

    public AniDbService()
    {
        _client = new(
            options: new()
            {
                ClientName = "nomercy",
                ClientVersion = 1,
                LocalPort = (ushort)(RuntimeServerSettings.Current.ExternalServerPort + 1),
            }
        );

        UserPass? userPass = CredentialManager.Credential(target: "AniDb");
        if (userPass == null)
            return;

        _username = userPass.Username;
        _password = userPass.Password;

        if (userPass.ApiKey == null)
            return;

        _apiKey = CredentialManager.ConvertToSecureString(password: userPass.ApiKey);
    }

    public void SetCredentials(string username, string password, string? apiKey)
    {
        _username = username;
        _password = password;

        if (apiKey == null)
            return;

        _apiKey = CredentialManager.ConvertToSecureString(password: apiKey);
    }

    public Task Init()
    {
        // Run on the thread pool so the connect/login finishes off-thread.
        return Task.Run(action: () =>
        {
            try
            {
                _client.Connect();
                _client.Login(callback: LoginCallback, username: _username, password: _password, api_key: _apiKey);
            }
            catch (Exception e)
            {
                Logger.AniDb(message: e.Message, level: LogEventLevel.Fatal);
                throw;
            }
        });
    }

    public async Task<AniDBAnimeItem> GetRandomAnime(CancellationToken ct = default)
    {
        TaskCompletionSource<AniDBAnimeItem> tcs = new();

        _client.FetchRandomAnime(
            callback: response =>
            {
                Logger.AniDb(message: response.StatusCode.ToString());
                Logger.AniDb(message: response.StatusMessage);

                response.GetMessageItem(
                    index: 0,
                    callback: new AniDbCallbackObject<AniDBAnimeItem>(callback: messageItem =>
                    {
                        messageItem.parseContentsDefault();
                        tcs.SetResult(result: messageItem);
                    })
                );
            },
            source: RandomAnimeSource.ANY,
            priority: 2
        );

        // Guard against the AniDB UDP callback never firing: time out after 10s
        // (linked to the caller's token) instead of awaiting the task forever.
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
        cts.CancelAfter(delay: TimeSpan.FromSeconds(seconds: 10));
        return await tcs.Task.WaitAsync(cancellationToken: cts.Token);
    }

    private static void LoginCallback(AniDBMessageResponse message)
    {
        Logger.AniDb(message: message, level: LogEventLevel.Debug);
    }

    private void LogoutCallback(AniDBMessageResponse message)
    {
        _client.Disconnect();
        _disconnectBarrier.SignalAndWait();
    }

    public void Dispose()
    {
        if (_client.IsConnected)
            try
            {
                _client.Logout(callback: LogoutCallback);
                _disconnectBarrier.SignalAndWait();
            }
            catch (Exception)
            {
                _client.Disconnect();
            }

        GC.SuppressFinalize(obj: this);
    }
}
