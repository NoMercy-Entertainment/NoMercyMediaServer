namespace NoMercy.Database.Models.Common;

public interface IHasLibrary
{
    Ulid LibraryId { get; set; }
    Library Library { get; set; }
}
