using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Filesystem operations for the dashboard folder picker. Uses
/// <see cref="IStorageBackend"/> directly (not <see cref="IStorage"/>)
/// because the picker legitimately browses paths the user hasn't added
/// to any library yet, so the path-guard allowlist on
/// <see cref="IStorage"/> doesn't apply here.
/// </summary>
public class FilesystemRepository(IStorageBackend backend)
{
    private readonly IStorageBackend _backend = backend;

    public (string? parent, List<DirectoryTree> entries) List(string folder, bool withEmpty)
    {
        if (string.IsNullOrEmpty(folder))
            return (null, []);

        if (!_backend.DirectoryExists(folder))
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

        if (string.IsNullOrEmpty(home) || !_backend.DirectoryExists(home))
            home = "/";

        (string? parent, List<DirectoryTree> entries) = List(home, withEmpty);
        return (home, parent, entries);
    }

    public List<DirectoryTree> Roots(bool withEmpty)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            return drives
                .Where(d => d.IsReady)
                .Select(d =>
                {
                    DirectoryTree entry = Build(d.RootDirectory.ToString(), "", withEmpty);
                    if (!string.IsNullOrEmpty(d.VolumeLabel))
                        entry.Subtitle = d.VolumeLabel;
                    return entry;
                })
                .OrderBy(e => e.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        (_, List<DirectoryTree> entries) = List("/", withEmpty);
        return entries;
    }

    public string Mkdir(string parent, string name)
    {
        if (string.IsNullOrWhiteSpace(parent))
            throw new ArgumentException("parent is required", nameof(parent));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name is required", nameof(name));

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("name contains invalid characters", nameof(name));

        if (!_backend.DirectoryExists(parent))
            throw new DirectoryNotFoundException($"parent does not exist: {parent}");

        string fullPath = Path.Combine(parent, name);
        if (_backend.DirectoryExists(fullPath))
            return fullPath;

        _backend.CreateDirectory(fullPath);
        return fullPath;
    }

    private DirectoryTree Build(string parent, string path, bool withEmpty)
    {
        DirectoryTree entry = new(parent, path);
        if (!withEmpty || entry.Type != "folder")
            return entry;

        try
        {
            entry.IsEmpty = !_backend
                .EnumerateFileSystemEntries(entry.FullPath, "*", SearchOption.TopDirectoryOnly)
                .Any();
        }
        catch (UnauthorizedAccessException)
        {
            entry.IsEmpty = null;
        }
        catch (IOException)
        {
            entry.IsEmpty = null;
        }

        return entry;
    }

    private IEnumerable<string> EnumerateChildDirectories(string folder)
    {
        foreach (
            string entry in _backend.EnumerateFileSystemEntries(
                folder,
                "*",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            if (!_backend.DirectoryExists(entry))
                continue;
            if (IsBrowsable(entry))
                yield return entry;
        }
    }

    private bool IsBrowsable(string path)
    {
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            return true;

        // Cross-platform name conventions: Unix dotfiles + Windows
        // sentinel folders (`$RECYCLE.BIN`, `$EXTEND`, ...).
        if (name.StartsWith('.') || name.StartsWith('$'))
            return false;

        // Windows Hidden / System attributes for everything else
        // (`System Volume Information`, `Recovery`, `Config.Msi`, ...).
        return !_backend.IsHidden(path);
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
