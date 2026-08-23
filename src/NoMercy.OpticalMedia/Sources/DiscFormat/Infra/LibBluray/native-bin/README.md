# Vendored libbluray native binaries

The five DLLs in this folder (`libbluray.dll`, `libaacs.dll`, `libbdplus.dll`,
`libgcrypt-20.dll`, `libgpg-error6-0.dll`) are the standard Windows build
stack for `libbluray`, used here only by `BlurayIdentityReader` to read a
disc's structural identity (no BD-J/JVM, no playback/overlay path).

## Licensing

All five projects are licensed **LGPL-2.1-or-later**:

- libbluray
- libaacs
- libbdplus
- libgcrypt
- libgpg-error

`NoMercy.OpticalMedia`'s managed wrapper (`LibBluray.cs`,
`LibBlurayResolver.cs`) calls into these via P/Invoke — dynamic linking,
which LGPL permits without imposing its terms on the calling code.
Redistributing the compiled binaries themselves (as this `.csproj` does by
copying them into build output) still requires the license text and
attribution to travel alongside them, which is what this file is for. See
<https://www.gnu.org/licenses/lgpl-2.1.html> for the full license text.

## Provenance — not yet verified

This is a placeholder attribution notice, **not** a checksum audit. The
exact upstream source, build, and version of each DLL in this vendoring
pass was not tracked or pinned. A follow-up should identify the precise
upstream release for each file and record its SHA-256 here (or in a
sibling manifest) so the binaries are reproducible and verifiable, not just
attributed.
