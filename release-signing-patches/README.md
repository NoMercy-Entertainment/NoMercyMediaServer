# Release-Signing Workflow Patches

This directory contains the updated `.github/workflows/` files that must be
applied to three companion repositories.  Each subdirectory mirrors the target
repo's path layout so the contents can be copied verbatim.

A reusable workflow that encapsulates the signing logic is published in this
repository as
[`.github/workflows/release-sign.yml`](../.github/workflows/release-sign.yml).

---

## Required org secrets

Add these **once** at the organisation level
(`Settings → Secrets and variables → Actions → New organisation secret`):

| Secret name                   | Value                                                       |
|-------------------------------|-------------------------------------------------------------|
| `GPG_SIGNING_KEY`             | ASCII-armored GPG private key: `gpg --armor --export-secret-key <KEY-ID>` |
| `GPG_SIGNING_KEY_PASSPHRASE`  | Passphrase for the key (omit entirely if the key is unprotected) |

---

## What each patch adds

### All three repos

| Added asset              | Description                                        |
|--------------------------|----------------------------------------------------|
| `<filename>.sha256`      | Per-asset hex digest sidecar (one file per asset)  |
| `manifest.json`          | JSON listing every asset with `name`, `sha256`, `size` |
| `manifest.json.sig`      | Detached ASCII-armored GPG signature of `manifest.json` |

### Manifest format

```json
{
  "repo": "NoMercy-Entertainment/<repo>",
  "tag": "v1.2.3",
  "created_at": "2025-01-01T00:00:00Z",
  "assets": [
    { "name": "ffmpeg-8.1.2-linux-x86_64-v1.2.3.tar.gz", "sha256": "abc…", "size": 12345678 }
  ]
}
```

---

## Applying the patches

For each target repo, copy the workflow file(s) shown below into the repo at
the same path and open a PR.

### nomercy-ffmpeg

```
nomercy-ffmpeg/
└── .github/workflows/main.yml   ← replace existing file
```

**Changes:** Added `Generate per-asset SHA256, manifest and signature` step to
both the `publish-rc` job (RC prereleases) and the `release` job (final
releases).  The step runs after staging and before uploading; the existing
`files: release/*` glob in `softprops/action-gh-release` picks up the new
files automatically.

### nomercy-tesseract

```
nomercy-tesseract/
└── .github/workflows/release.yml   ← replace existing file
```

**Changes:**
- Added `Generate per-asset SHA256, manifest and signature` step between
  `Determine tag name` and `Create Release`.
- Replaced the long explicit file list in `Create Release` with glob patterns
  (`tessdata/*.traineddata`, `tessdata/*.traineddata.sha256`, `manifest.json`,
  `manifest.json.sig`) so new training-data files are included automatically.

### nomercy-whisper-models

```
nomercy-whisper-models/
├── .github/workflows/main.yml          ← replace existing file
└── .github/workflows/build-model.yml  ← replace existing file
```

**Changes to `build-model.yml`:**
- Added `Generate SHA256 for model file(s)` step after splitting large models.
- Added `Upload SHA256 as workflow artifact` step so the `sign-manifest` job
  can read hashes without re-downloading multi-GB model files.
- Updated `Set upload pattern` and `Upload model(s) … to GitHub Release` to
  also upload the `.sha256` sidecar(s) alongside each model.

**Changes to `main.yml`:**
- Added `sign-manifest` job that waits for all four model builds, downloads the
  SHA256 artifacts, builds `manifest.json`, signs it, and uploads both files.
- `publish-release` now depends on `sign-manifest` so the manifest is always
  present when the draft is made public.
