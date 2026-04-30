using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Filesystem operations for the dashboard folder picker. Walks the raw
/// filesystem so users can browse outside their library scopes when
/// configuring new mounts.
/// </summary>
public class FilesystemRepository
{
    public (string? parent, List<DirectoryTree> entries) List(string folder, bool withEmpty)
    {
        if (string.IsNullOrEmpty(folder))
            return (null, []);

        if (!Directory.Exists(folder))
            return (null, []);

        List<DirectoryTree> entries;
        try
        {
            entries = EnumerateChildDirectories(folder)
                .Select(child => Build(folder, child, withEmpty))
                .OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (IOException)
        {
            return (ParentOf(folder), []);
        }
        catch (UnauthorizedAccessException)
        {
            return (ParentOf(folder), []);
        }

        return (ParentOf(folder), entries);
    }

    public (string path, string? parent, List<DirectoryTree> entries) Home(bool withEmpty)
    {
        string home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolderOption.DoNotVerify
        );

        if (string.IsNullOrEmpty(home) || !Directory.Exists(home))
            home = "/";

        (string? parent, List<DirectoryTree> entries) = List(home, withEmpty);
        return (home, parent, entries);
    }

    public List<DirectoryTree> Roots(bool withEmpty)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Skip drive types whose metadata reads can block on OS-level
            // retry timeouts (a dead Z:\ network share or empty CD-ROM
            // adds ~30s each). Users type those paths into /ls directly.
            DriveInfo[] drives = DriveInfo.GetDrives();
            return drives
                .Where(d =>
                    d.DriveType != DriveType.Network
                    && d.DriveType != DriveType.Unknown
                    && d.DriveType != DriveType.NoRootDirectory
                )
                .AsParallel()
                .Select(BuildRoot)
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .OrderBy(e => e.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        (_, List<DirectoryTree> entries) = List("/", withEmpty);
        return entries;
    }

    private static DirectoryTree? BuildRoot(DriveInfo drive)
    {
        try
        {
            if (!drive.IsReady)
                return null;

            DirectoryTree entry = Build(drive.RootDirectory.ToString(), "", withEmpty: false);

            try
            {
                if (!string.IsNullOrEmpty(drive.VolumeLabel))
                    entry.Subtitle = drive.VolumeLabel;
            }
            catch
            {
                // VolumeLabel can throw on slow/transitional drives; we
                // already have a valid entry, just skip the label.
            }

            return entry;
        }
        catch
        {
            return null;
        }
    }

    public string Mkdir(string parent, string name)
    {
        if (string.IsNullOrWhiteSpace(parent))
            throw new ArgumentException("parent is required", nameof(parent));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name is required", nameof(name));

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("name contains invalid characters", nameof(name));

        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException($"parent does not exist: {parent}");

        string fullPath = Path.Combine(parent, name);
        if (Directory.Exists(fullPath))
            return fullPath;

        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private static DirectoryTree Build(string parent, string path, bool withEmpty)
    {
        // withEmpty intentionally ignored — populating IsEmpty per entry
        // means an extra TopDirectoryOnly enumeration for every child
        // folder, which kills picker latency on big trees. Clients can
        // still send the flag for wire compat but the field stays null.
        _ = withEmpty;
        return new(parent, path);
    }

    private static IEnumerable<string> EnumerateChildDirectories(string folder)
    {
        foreach (
            string entry in Directory.EnumerateFileSystemEntries(
                folder,
                "*",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            if (!Directory.Exists(entry))
                continue;
            if (IsBrowsable(entry))
                yield return entry;
        }
    }

    private static bool IsBrowsable(string path)
    {
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            return true;

        // Cross-platform name conventions: Unix dotfiles + Windows
        // sentinel folders (`$RECYCLE.BIN`, `$EXTEND`, ...).
        if (name.StartsWith('.') || name.StartsWith('$'))
            return false;

        return !IsHidden(path);
    }

    private static bool IsHidden(string path)
    {
        try
        {
            FileAttributes attrs = File.GetAttributes(path);
            return (attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? ParentOf(string folder)
    {
        if (string.IsNullOrEmpty(folder) || folder == "/")
            return null;

        try
        {
            DirectoryInfo info = new(folder);
            return info.Parent?.FullName;
        }
        catch
        {
            return null;
        }
    }
}
