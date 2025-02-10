using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NoMercy.Database;
public class UlidToStringConverter : ValueConverter<Ulid, string>
{
    private static readonly ConverterMappingHints DefaultHints = new(26);

    public UlidToStringConverter() : this(null)
    {
    }

    private UlidToStringConverter(ConverterMappingHints? mappingHints = null)
        : base(
            x => x.ToString(),
            x => Ulid.Parse(x),
            DefaultHints.With(mappingHints))
    {
    }
}