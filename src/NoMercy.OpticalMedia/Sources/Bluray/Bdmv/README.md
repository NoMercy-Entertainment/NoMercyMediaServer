# Vendored from packages/nomercy-disc-format

`Mpls.cs`, `MplsParser.cs`, `CodingType.cs`, `CodingTypeInfo.cs`, `CodingKind.cs`,
`BigEndianReader.cs` are copied
verbatim (same namespace, `NoMercy.DiscFormat.Disc.Bdmv`) from
`packages/nomercy-disc-format/src/Infra/Disc/Bdmv/` rather than referenced
via `ProjectReference`.

**Why:** that repo has no GitHub remote — it's local-only, mid-development
on `feat/nmdf` — so a cross-repo `ProjectReference` to it builds fine on a
machine with the whole monorepo checked out, but breaks every CI run,
which clones only `nomercy-media-server` in isolation. Confirmed broken in
practice: CI failed with `The type or namespace name 'DiscFormat' does not
exist` the first time this dependency shipped.

These six files are fully self-contained (no further `NoMercy.*`
dependencies beyond each other), so vendoring them is a clean copy, not a
partial one.

**If nomercy-disc-format ever gets a GitHub remote or ships as a NuGet
package**, delete this folder and its callers' `using
NoMercy.DiscFormat.Disc.Bdmv;` keep working unchanged — swap this
`ProjectReference`/vendored-copy split for a real package reference
instead. Until then, keep both copies in sync by hand if the parser
changes upstream.
