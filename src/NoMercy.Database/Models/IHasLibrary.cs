namespace NoMercy.Database.Models;

public interface IHasLibrary
{
    Ulid LibraryId { get; set; }
    Library Library { get; set; }
}
