using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Filesystem operations for the dashboard folder picker. Uses
/// <see cref="IStorageDriver"/> directly (not <see cref="IStorage"/>)
/// because the picker legitimately browses paths the user hasn't added
/// to any library yet, so the path-guard allowlist on
/// <see cref="IStorage"/> doesn't apply here.
/// </summary>
public class FilesystemRepository(IStorageDriver driver)
{
    private readonly IStorageDriver _driver = driver;

    public (string? parent, List<DirectoryTree> entries) List(string folder, bool withEmpty)
    {
        if (string.IsNullOrEmpty(folder))
            return (null, []);

        if (!_driver.DirectoryExists(folder))
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

        if (string.IsNullOrEmpty(home) || !_driver.DirectoryExists(home))
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

    private DirectoryTree? BuildRoot(DriveInfo drive)
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

        if (!_driver.DirectoryExists(parent))
            throw new DirectoryNotFoundException($"parent does not exist: {parent}");

        string fullPath = Path.Combine(parent, name);
        if (_driver.DirectoryExists(fullPath))
            return fullPath;

        _driver.CreateDirectory(fullPath);
        return fullPath;
    }

    private DirectoryTree Build(string parent, string path, bool withEmpty)
    {
        // withEmpty intentionally ignored — populating IsEmpty per entry
        // means an extra TopDirectoryOnly enumeration for every child
        // folder, which kills picker latency on big trees. Clients can
        // still send the flag for wire compat but the field stays null.
        _ = withEmpty;
        return new(parent, path);
    }

    private IEnumerable<string> EnumerateChildDirectories(string folder)
    {
        foreach (
            string entry in _driver.EnumerateFileSystemEntries(
                folder,
                "*",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            if (!_driver.DirectoryExists(entry))
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
        return !_driver.IsHidden(path);
    }

    private static string? ParentOf(string folder)
    {
        if (string.IsNullOrEmpty(folder) || folder == "/")
            return null;

        try
        {
            return Path.GetDirectoryName(
                folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            );
        }
        catch
        {
            return null;
        }
    }
}
