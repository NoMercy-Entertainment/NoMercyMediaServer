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

namespace NoMercy.Storage.Drivers.Nfs.Interop;

/// <summary>
/// Production <see cref="ILibNfs"/> implementation. Forwards each call
/// straight through to the corresponding static <see cref="LibNfs"/> P/Invoke
/// wrapper — no behaviour change, just a virtual dispatch seam so tests can
/// substitute a fault-injecting fake.
/// </summary>
internal sealed class LibNfsPInvoke : ILibNfs
{
    public static readonly LibNfsPInvoke Instance = new();

    public IntPtr InitContext() => LibNfs.InitContext();

    public void DestroyContext(IntPtr nfs) => LibNfs.DestroyContext(nfs: nfs);

    public int Mount(IntPtr nfs, string server, string exportPath) =>
        LibNfs.Mount(nfs: nfs, server: server, exportPath: exportPath);

    public int Umount(IntPtr nfs) => LibNfs.Umount(nfs: nfs);

    public void SetUid(IntPtr nfs, int uid) => LibNfs.SetUid(nfs: nfs, uid: uid);

    public void SetGid(IntPtr nfs, int gid) => LibNfs.SetGid(nfs: nfs, gid: gid);

    public int SetVersion(IntPtr nfs, int version) => LibNfs.SetVersion(nfs: nfs, version: version);

    public void SetClientName(IntPtr nfs, string id) => LibNfs.SetClientName(nfs: nfs, id: id);

    public void SetVerifier(IntPtr nfs, string verifier) => LibNfs.SetVerifier(nfs: nfs, verifier: verifier);

    public string GetError(IntPtr nfs) => LibNfs.GetError(nfs: nfs);

    public int Stat64(IntPtr nfs, string path, out LibNfs.NfsStat64 stat) =>
        LibNfs.Stat64(nfs: nfs, path: path, stat: out stat);

    public int Lstat64(IntPtr nfs, string path, out LibNfs.NfsStat64 stat) =>
        LibNfs.Lstat64(nfs: nfs, path: path, stat: out stat);

    public int OpenDir(IntPtr nfs, string path, out IntPtr dir) =>
        LibNfs.OpenDir(nfs: nfs, path: path, dir: out dir);

    public IntPtr ReadDir(IntPtr nfs, IntPtr dir) => LibNfs.ReadDir(nfs: nfs, dir: dir);

    public void CloseDir(IntPtr nfs, IntPtr dir) => LibNfs.CloseDir(nfs: nfs, dir: dir);

    public int MkDir(IntPtr nfs, string path) => LibNfs.MkDir(nfs: nfs, path: path);

    public int RmDir(IntPtr nfs, string path) => LibNfs.RmDir(nfs: nfs, path: path);

    public int Open(IntPtr nfs, string path, int flags, out IntPtr fh) =>
        LibNfs.Open(nfs: nfs, path: path, flags: flags, fh: out fh);

    public int Creat(IntPtr nfs, string path, int mode, out IntPtr fh) =>
        LibNfs.Creat(nfs: nfs, path: path, mode: mode, fh: out fh);

    public int Close(IntPtr nfs, IntPtr fh) => LibNfs.Close(nfs: nfs, fh: fh);

    public int Read(IntPtr nfs, IntPtr fh, IntPtr buf, int count) =>
        LibNfs.Read(nfs: nfs, fh: fh, buf: buf, count: count);

    public int Write(IntPtr nfs, IntPtr fh, IntPtr buf, int count) =>
        LibNfs.Write(nfs: nfs, fh: fh, buf: buf, count: count);

    public long Lseek(IntPtr nfs, IntPtr fh, long offset, int whence, out ulong currentOffset) =>
        LibNfs.Lseek(nfs: nfs, fh: fh, offset: offset, whence: whence, currentOffset: out currentOffset);

    public int Unlink(IntPtr nfs, string path) => LibNfs.Unlink(nfs: nfs, path: path);

    public int Rename(IntPtr nfs, string oldPath, string newPath) =>
        LibNfs.Rename(nfs: nfs, oldPath: oldPath, newPath: newPath);

    public int Readlink(IntPtr nfs, string path, IntPtr buf, int bufSize) =>
        LibNfs.Readlink(nfs: nfs, path: path, buf: buf, bufSize: bufSize);

    public IntPtr MountGetExports(string server) => LibNfs.MountGetExports(server: server);

    public void MountFreeExportList(IntPtr exports) => LibNfs.MountFreeExportList(exports: exports);
}
