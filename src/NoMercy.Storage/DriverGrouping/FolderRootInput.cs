namespace NoMercy.Storage.DriverGrouping;

/// <summary>
/// Input descriptor for the pure grouping function.
/// </summary>
/// <param name="FolderId">Stable identifier for the folder.</param>
/// <param name="AbsoluteRootPath">
/// The folder's current absolute path on disk — either the old V1
/// <c>Folder.Path</c> (a full absolute path) or the per-folder driver's
/// <c>Config.rootPath</c> when the earlier migration has already run.
/// </param>
public sealed record FolderRootInput(Ulid FolderId, string AbsoluteRootPath);
