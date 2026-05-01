using System.Runtime.InteropServices;

namespace NoMercy.Storage.Drivers.Nfs.Interop;

/// <summary>
/// P/Invoke bindings for libnfs (https://github.com/sahlberg/libnfs).
/// MIT-licensed C library — cross-platform NFS3/NFS4 client without OS mount.
///
/// Native binary locations (resolved by .NET runtime DLL search):
///   Windows  → runtimes/win-x64/native/libnfs.dll
///   Linux    → runtimes/linux-x64/native/libnfs.so
///            → runtimes/linux-arm64/native/libnfs.so
///   macOS    → runtimes/osx-x64/native/libnfs.dylib
///            → runtimes/osx-arm64/native/libnfs.dylib
///
/// For development without the NuGet package, install the system library:
///   Linux:   apt install libnfs-dev  (or dnf/pacman equivalent)
///   macOS:   brew install libnfs
///   Windows: pre-built DLLs from the libnfs GitHub releases page
/// </summary>
internal static class LibNfs
{
    private const string LibName = "nfs";

    // -----------------------------------------------------------------------
    // Context lifecycle
    // -----------------------------------------------------------------------

    [DllImport(
        LibName,
        EntryPoint = "nfs_init_context",
        CallingConvention = CallingConvention.Cdecl
    )]
    internal static extern IntPtr InitContext();

    [DllImport(
        LibName,
        EntryPoint = "nfs_destroy_context",
        CallingConvention = CallingConvention.Cdecl
    )]
    internal static extern void DestroyContext(IntPtr nfs);

    // -----------------------------------------------------------------------
    // Mount
    // -----------------------------------------------------------------------

    [DllImport(
        LibName,
        EntryPoint = "nfs_mount",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int Mount(IntPtr nfs, string server, string exportPath);

    [DllImport(LibName, EntryPoint = "nfs_umount", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Umount(IntPtr nfs);

    // -----------------------------------------------------------------------
    // Auth
    // -----------------------------------------------------------------------

    [DllImport(LibName, EntryPoint = "nfs_set_uid", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetUid(IntPtr nfs, int uid);

    [DllImport(LibName, EntryPoint = "nfs_set_gid", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetGid(IntPtr nfs, int gid);

    // -----------------------------------------------------------------------
    // Error
    // -----------------------------------------------------------------------

    [DllImport(LibName, EntryPoint = "nfs_get_error", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetErrorPtr(IntPtr nfs);

    internal static string GetError(IntPtr nfs)
    {
        IntPtr ptr = GetErrorPtr(nfs);
        return ptr == IntPtr.Zero
            ? "unknown error"
            : Marshal.PtrToStringAnsi(ptr) ?? "unknown error";
    }

    // -----------------------------------------------------------------------
    // Stat / attribute
    // -----------------------------------------------------------------------

    [DllImport(
        LibName,
        EntryPoint = "nfs_stat64",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int Stat64(IntPtr nfs, string path, out NfsStat64 stat);

    [DllImport(
        LibName,
        EntryPoint = "nfs_lstat64",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int Lstat64(IntPtr nfs, string path, out NfsStat64 stat);

    // -----------------------------------------------------------------------
    // Directory
    // -----------------------------------------------------------------------

    [DllImport(
        LibName,
        EntryPoint = "nfs_opendir",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int OpenDir(IntPtr nfs, string path, out IntPtr dir);

    [DllImport(LibName, EntryPoint = "nfs_readdir", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ReadDir(IntPtr nfs, IntPtr dir);

    [DllImport(LibName, EntryPoint = "nfs_closedir", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void CloseDir(IntPtr nfs, IntPtr dir);

    [DllImport(
        LibName,
        EntryPoint = "nfs_mkdir",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int MkDir(IntPtr nfs, string path);

    [DllImport(
        LibName,
        EntryPoint = "nfs_rmdir",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int RmDir(IntPtr nfs, string path);

    // -----------------------------------------------------------------------
    // File I/O
    // -----------------------------------------------------------------------

    [DllImport(
        LibName,
        EntryPoint = "nfs_open",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int Open(IntPtr nfs, string path, int flags, out IntPtr fh);

    [DllImport(
        LibName,
        EntryPoint = "nfs_creat",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int Creat(IntPtr nfs, string path, int mode, out IntPtr fh);

    [DllImport(LibName, EntryPoint = "nfs_close", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Close(IntPtr nfs, IntPtr fh);

    [DllImport(LibName, EntryPoint = "nfs_read", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Read(IntPtr nfs, IntPtr fh, IntPtr buf, int count);

    [DllImport(LibName, EntryPoint = "nfs_write", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Write(IntPtr nfs, IntPtr fh, IntPtr buf, int count);

    [DllImport(LibName, EntryPoint = "nfs_lseek", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long Lseek(
        IntPtr nfs,
        IntPtr fh,
        long offset,
        int whence,
        out ulong currentOffset
    );

    [DllImport(
        LibName,
        EntryPoint = "nfs_unlink",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int Unlink(IntPtr nfs, string path);

    [DllImport(
        LibName,
        EntryPoint = "nfs_rename",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int Rename(IntPtr nfs, string oldPath, string newPath);

    // -----------------------------------------------------------------------
    // Symlink
    // -----------------------------------------------------------------------

    [DllImport(
        LibName,
        EntryPoint = "nfs_readlink",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi
    )]
    internal static extern int Readlink(IntPtr nfs, string path, IntPtr buf, int bufSize);

    // -----------------------------------------------------------------------
    // POSIX flags
    // -----------------------------------------------------------------------

    internal const int O_RDONLY = 0;
    internal const int O_WRONLY = 1;
    internal const int O_RDWR = 2;
    internal const int O_CREAT = 0x40;
    internal const int O_TRUNC = 0x200;
    internal const int O_EXCL = 0x80;

    internal const int DefaultFileMode = 0x1A4; // 0644

    // -----------------------------------------------------------------------
    // NFS file type constants (from nfsproto.h)
    // -----------------------------------------------------------------------

    internal const int NF3REG = 1;
    internal const int NF3DIR = 2;
    internal const int NF3LNK = 5;

    // -----------------------------------------------------------------------
    // Structs
    // -----------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct NfsStat64
    {
        public ulong DevId;
        public ulong Ino;
        public ulong Mode;
        public ulong Nlink;
        public ulong Uid;
        public ulong Gid;
        public ulong Rdev;
        public ulong Size;
        public ulong BlkSize;
        public ulong Blocks;
        public ulong AtimeSec;
        public ulong AtimeNsec;
        public ulong MtimeSec;
        public ulong MtimeNsec;
        public ulong CtimeSec;
        public ulong CtimeNsec;

        /// <summary>
        /// File type extracted from Mode (top 4 bits, shifted right 12).
        /// </summary>
        internal int FileType => (int)((Mode >> 12) & 0xF);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct NfsDirent
    {
        public long Inode;
        public long Offset;
        public uint RecLen;
        public byte Type;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Name;
    }
}
