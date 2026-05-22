using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// BuiltinPresetSeeder upserts every built-in preset on startup and prunes
/// orphan built-in rows whose Ulids no longer match the shipped set. Without
/// this seeder, users would be stuck with whatever built-ins shipped in the
/// version they first installed.
/// </summary>
public class BuiltinPresetSeederTests
{
    private static MediaContext NewContext()
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase($"seeder-{Ulid.NewUlid()}")
            .Options;
        return new MediaContext(options);
    }

    [Fact]
    public async Task SeedAsync_EmptyDb_InsertsEveryBuiltin()
    {
        // First boot: the encoder_presets table is empty. The seeder inserts
        // exactly the set returned by BuiltinPresets.All() — no more, no less.
        await using MediaContext ctx = NewContext();
        BuiltinPresetSeeder subject = new(ctx);

        await subject.SeedAsync();

        EncodingProfile[] expected = BuiltinPresets.All();
        List<EncodingPreset> seeded = await ctx.EncodingPresets.ToListAsync();
        seeded.Should().HaveCount(expected.Length);
        seeded.Should().OnlyContain(p => p.IsBuiltIn);
        seeded.Should().OnlyContain(p => p.Source == "builtin");
        seeded.Select(p => p.Id).Should().BeEquivalentTo(expected.Select(p => p.Id));
    }

    [Fact]
    public async Task SeedAsync_ExistingMatchingBuiltin_UpdatesInPlace()
    {
        // A built-in row already exists with stale name/description (e.g.
        // the user installed v1.0 and we just shipped v1.1 with renamed copy).
        // The seeder must UPDATE in place, not insert a duplicate.
        await using MediaContext ctx = NewContext();
        EncodingProfile firstBuiltin = BuiltinPresets.All()[0];

        ctx.EncodingPresets.Add(
            new EncodingPreset
            {
                Id = firstBuiltin.Id,
                Name = "Stale Name",
                Description = "Stale Description",
                ProfileJson = "{}",
                IsBuiltIn = true,
                Source = "builtin",
            }
        );
        await ctx.SaveChangesAsync();

        BuiltinPresetSeeder subject = new(ctx);
        await subject.SeedAsync();

        EncodingPreset? row = await ctx.EncodingPresets.FirstOrDefaultAsync(p =>
            p.Id == firstBuiltin.Id
        );
        row.Should().NotBeNull();
        row!.Name.Should().Be(firstBuiltin.Name);
        row.Description.Should().Be(firstBuiltin.Description);
        row.ProfileJson.Should().NotBe("{}");
    }

    [Fact]
    public async Task SeedAsync_StaleBuiltinRow_IsDropped()
    {
        // A stale built-in row (from a previous shipped version) remains in
        // the DB. Its Ulid is not in the current builtins set — seeder must
        // remove it so the dashboard list matches reality.
        await using MediaContext ctx = NewContext();
        Ulid staleId = Ulid.NewUlid();
        ctx.EncodingPresets.Add(
            new EncodingPreset
            {
                Id = staleId,
                Name = "Removed Built-in",
                Description = "Was shipped in v0.9, no longer ships",
                ProfileJson = "{}",
                IsBuiltIn = true,
                Source = "builtin",
            }
        );
        await ctx.SaveChangesAsync();

        BuiltinPresetSeeder subject = new(ctx);
        await subject.SeedAsync();

        bool exists = await ctx.EncodingPresets.AnyAsync(p => p.Id == staleId);
        exists.Should().BeFalse("stale built-in rows must be pruned on every seed");
    }

    [Fact]
    public async Task SeedAsync_UserCustomPreset_IsLeftAlone()
    {
        // User-authored preset (IsBuiltIn=false) MUST survive every seed run.
        // Dropping these would lose user customization on every upgrade.
        await using MediaContext ctx = NewContext();
        Ulid userId = Ulid.NewUlid();
        ctx.EncodingPresets.Add(
            new EncodingPreset
            {
                Id = userId,
                Name = "My Custom",
                Description = "user-made",
                ProfileJson = "{}",
                IsBuiltIn = false,
                Source = "user",
            }
        );
        await ctx.SaveChangesAsync();

        BuiltinPresetSeeder subject = new(ctx);
        await subject.SeedAsync();

        EncodingPreset? row = await ctx.EncodingPresets.FirstOrDefaultAsync(p => p.Id == userId);
        row.Should().NotBeNull();
        row!.Name.Should().Be("My Custom");
        row.IsBuiltIn.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotent()
    {
        // Re-running the seeder must not duplicate rows or change content.
        await using MediaContext ctx = NewContext();
        BuiltinPresetSeeder subject = new(ctx);

        await subject.SeedAsync();
        int countAfterFirst = await ctx.EncodingPresets.CountAsync();
        await subject.SeedAsync();
        int countAfterSecond = await ctx.EncodingPresets.CountAsync();

        countAfterSecond.Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task SeedAsync_StoresProfileJsonForLookup()
    {
        // The seeder must persist the SERIALIZED profile so DbPresetLookup
        // can deserialize and PresetResolver can resolve inheritance. Empty
        // JSON would break the entire preset chain.
        await using MediaContext ctx = NewContext();
        EncodingProfile firstBuiltin = BuiltinPresets.All()[0];
        BuiltinPresetSeeder subject = new(ctx);

        await subject.SeedAsync();

        EncodingPreset? row = await ctx.EncodingPresets.FirstOrDefaultAsync(p =>
            p.Id == firstBuiltin.Id
        );
        row.Should().NotBeNull();
        row!.ProfileJson.Should().NotBeNullOrWhiteSpace();
        row.ProfileJson.Should().Contain(firstBuiltin.Name);
    }
}
