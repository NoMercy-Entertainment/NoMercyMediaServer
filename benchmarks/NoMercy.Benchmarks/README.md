# NoMercy.Benchmarks

Query-level performance benchmarks that run against a **real** NoMercy database, so
the numbers reflect production-scale data (a multi-gigabyte `media.db` with real
libraries) rather than a synthetic fixture. Use it to find slow repository queries,
then to prove an optimization actually moved the needle.

Each case times a single repository method. Every query is read-only
(`AsNoTracking`); the benchmark never writes, so it is safe to run against a live
database while the server is up (SQLite WAL allows concurrent readers).

## Running

Build in Release for reliable numbers, then run:

```bash
dotnet run -c Release --project benchmarks/NoMercy.Benchmarks
```

By default it binds to the dev data root (`%LOCALAPPDATA%/NoMercy_dev` or
`~/.local/share/NoMercy_dev`). Point it at any data directory with `--app-path`:

```bash
dotnet run -c Release --project benchmarks/NoMercy.Benchmarks -- \
  --app-path "/path/to/NoMercy" --samples 5 --filter genres
```

### Options

| flag | default | meaning |
| --- | --- | --- |
| `--app-path` | dev root | app-data root; its `data/media.db` is benchmarked |
| `--samples` | `3` | warm runs per case (a cold run is always measured first) |
| `--filter` | none | substring match on case name; run a subset |
| `--user` | test user | user GUID the queries scope to (library access) |
| `--library` | seeded ULID | library id for the library-cards case |
| `--artist` | seeded GUID | artist id for the music-artist case |
| `--language` / `--country` | `en` / `NL` | locale passed to the queries |
| `--json` | `benchmark-results.json` | where the machine-readable results are written |

## Methodology

Each case runs once **cold** (query-plan compilation plus a cold page cache) and
then `--samples` times **warm**, each on a fresh `DbContext` — the same shape a
request gets. The report shows cold, warm p50 and warm p95. A large cold/warm gap
points at plan-compilation or cold-IO cost; a high warm p50 points at the query
itself doing too much work.

`rows` is the count each query returned, so a query that looks fast only because it
silently returned nothing is obvious in the output.

## Adding a case

Add a `BenchmarkCase` to the list in `Program.cs`. Create the repository with the
`MediaContextFactory` (or a fresh `MediaContext` for repositories that take one
directly) and return the row count the query produced.

## What it currently covers

The seeded cases target the routes a latency sweep flagged as the heaviest against
real data: the genre grid, the home feeds, the screensaver rotation set, library TV
cards, and a music artist page. `genres` is the headline offender — its
count-per-genre projection issues nested correlated subqueries that SQLite cannot
optimize.
