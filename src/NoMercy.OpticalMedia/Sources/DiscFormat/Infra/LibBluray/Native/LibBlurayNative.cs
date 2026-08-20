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

using System.Runtime.InteropServices;

namespace NoMercy.DiscFormat.LibBluray.Native;

/// <summary>
/// Raw P/Invoke bindings for libbluray.dll.
/// All entry points match the signatures in bluray.h / overlay.h exactly.
/// </summary>
internal static partial class LibBlurayNative
{
    private const string LibName = "libbluray";

    // -------------------------------------------------------------------------
    // Core disc lifecycle
    // -------------------------------------------------------------------------

    [LibraryImport(LibName, EntryPoint = "bd_open", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint BdOpen(string devicePath, string? keyfilePath);

    [LibraryImport(LibName, EntryPoint = "bd_close")]
    internal static partial void BdClose(nint bd);

    [LibraryImport(LibName, EntryPoint = "bd_get_disc_info")]
    internal static partial nint BdGetDiscInfo(nint bd);

    [LibraryImport(LibName, EntryPoint = "bd_get_titles")]
    internal static partial uint BdGetTitles(nint bd, byte flags, uint minTitleLength);

    [LibraryImport(LibName, EntryPoint = "bd_get_title_info")]
    internal static partial nint BdGetTitleInfo(nint bd, uint titleIdx, uint angle);

    // Playlist info for the playlist currently playing — used to read the active playlist when a button
    // starts playback via playitem/chapter without a fresh BD_EVENT_PLAYLIST.
    [LibraryImport(LibName, EntryPoint = "bd_get_playlist_info")]
    internal static partial nint BdGetPlaylistInfo(nint bd, uint playlist, uint angle);

    // The title index currently selected (its BLURAY_TITLE_INFO.playlist is the active playlist).
    [LibraryImport(LibName, EntryPoint = "bd_get_current_title")]
    internal static partial uint BdGetCurrentTitle(nint bd);

    [LibraryImport(LibName, EntryPoint = "bd_free_title_info")]
    internal static partial void BdFreeTitleInfo(nint titleInfo);

    // Index of the longest title in the list built by bd_get_titles (-1 on error).
    [LibraryImport(LibName, EntryPoint = "bd_get_main_title")]
    internal static partial int BdGetMainTitle(nint bd);

    // Switch to title playback mode; subsequent bd_read returns decrypted units.
    [LibraryImport(LibName, EntryPoint = "bd_select_title")]
    internal static partial int BdSelectTitle(nint bd, uint title);

    [LibraryImport(LibName, EntryPoint = "bd_select_playlist")]
    internal static partial int BdSelectPlaylist(nint bd, uint playlist);

    [LibraryImport(LibName, EntryPoint = "bd_get_title_size")]
    internal static partial long BdGetTitleSize(nint bd);

    // -------------------------------------------------------------------------
    // Read / seek
    // -------------------------------------------------------------------------

    [LibraryImport(LibName, EntryPoint = "bd_read")]
    internal static partial int BdRead(nint bd, nint buf, int len);

    // bd_read_ext drives the HDMV VM on each call (required for nav-mode).
    // Returns bytes read; event is filled with the next queued nav event.
    [LibraryImport(LibName, EntryPoint = "bd_read_ext")]
    internal static partial int BdReadExt(
        nint bd,
        nint buf,
        int len,
        ref BlurayEventNative outEvent
    );

    // After BD_EVENT_STILL_TIME the playlist pauses on a still; the menu Xlet waits for the player to
    // release it. Without this call the menu freezes on LOADING forever — this is what VLC does to
    // drive a menu past its still loop.
    [LibraryImport(LibName, EntryPoint = "bd_read_skip_still")]
    internal static partial int BdReadSkipStill(nint bd);

    // -------------------------------------------------------------------------
    // Navigation-mode playback (declared for next task, not driven here)
    // -------------------------------------------------------------------------

    [LibraryImport(LibName, EntryPoint = "bd_play")]
    internal static partial int BdPlay(nint bd);

    [LibraryImport(LibName, EntryPoint = "bd_menu_call")]
    internal static partial int BdMenuCall(nint bd, long pts);

    [LibraryImport(LibName, EntryPoint = "bd_get_event")]
    internal static partial int BdGetEvent(nint bd, ref BlurayEventNative bdEvent);

    [LibraryImport(LibName, EntryPoint = "bd_user_input")]
    internal static partial int BdUserInput(nint bd, long pts, uint key);

    // -------------------------------------------------------------------------
    // Player settings
    // -------------------------------------------------------------------------

    [LibraryImport(LibName, EntryPoint = "bd_set_player_setting")]
    internal static partial int BdSetPlayerSetting(nint bd, uint idx, uint value);

    [LibraryImport(
        LibName,
        EntryPoint = "bd_set_player_setting_str",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial int BdSetPlayerSettingStr(nint bd, uint idx, string? value);

    // -------------------------------------------------------------------------
    // Overlay registration (declared for next task, not driven here)
    // -------------------------------------------------------------------------

    [LibraryImport(LibName, EntryPoint = "bd_register_overlay_proc")]
    internal static partial void BdRegisterOverlayProc(
        nint bd,
        nint handle,
        BdOverlayProcDelegate? func
    );

    [LibraryImport(LibName, EntryPoint = "bd_register_argb_overlay_proc")]
    internal static partial void BdRegisterArgbOverlayProc(
        nint bd,
        nint handle,
        BdArgbOverlayProcDelegate? func,
        nint buf
    );
}
