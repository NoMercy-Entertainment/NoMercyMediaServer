# Plan: Self-Signed Code Signing for Windows and macOS

## Context

Sign Windows and macOS binaries with a self-signed certificate to prove they originate from the same developer — similar to how Android allows self-signed APK signing. This won't eliminate OS warnings (SmartScreen/Gatekeeper) but provides:
- **Consistency**: Every release is signed with the same key, proving same origin
- **Integrity**: Signatures prove binaries haven't been tampered with after signing
- **Identity**: Users can inspect the digital signature to verify the publisher ("NoMercy Entertainment")

Currently, no code signing exists for Windows or macOS. Only Linux RPM packages are GPG-signed.

## Certificate Generation (one-time manual step)

Generate a single self-signed code signing certificate (PFX/P12) using OpenSSL. Run locally:

```bash
# Generate self-signed code signing certificate (valid 10 years)
openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 \
  -keyout key.pem -out cert.pem -nodes \
  -subj "/CN=NoMercy Entertainment/O=NoMercy Entertainment" \
  -addext "extendedKeyUsage=codeSigning"

# Package as PFX (used by both Windows signtool and macOS keychain)
openssl pkcs12 -export -out nomercy-codesign.pfx -inkey key.pem -in cert.pem

# Base64-encode for GitHub secrets
base64 -i nomercy-codesign.pfx > nomercy-codesign.pfx.b64
```

## GitHub Secrets to Add

| Secret | Value |
|--------|-------|
| `CODE_SIGNING_CERT_BASE64` | Base64-encoded PFX file |
| `CODE_SIGNING_CERT_PASSWORD` | PFX password |

## Files to Modify

### 1. `.github/actions/build-dotnet-project/action.yml` — Sign standalone executables

This action runs on **macOS-latest** and cross-compiles for all platforms.

**Add inputs:**
- `signing-cert-base64` (optional, default empty)
- `signing-cert-password` (optional, default empty)

**Add steps** (after builds, before artifact upload):

**a) Sign Windows .exe with `osslsigncode`** (before rename to `-windows-x64.exe`):
```bash
brew install osslsigncode
echo "$CERT_BASE64" | base64 -d > /tmp/cert.pfx
osslsigncode sign -pkcs12 /tmp/cert.pfx -pass "$CERT_PASSWORD" \
  -n "NoMercy MediaServer" -i "https://nomercy.tv" \
  -t http://timestamp.digicert.com \
  -in "./output/$PROJECT_NAME.exe" -out "./output/$PROJECT_NAME-signed.exe"
mv "./output/$PROJECT_NAME-signed.exe" "./output/$PROJECT_NAME.exe"
rm /tmp/cert.pfx
```

**b) Sign macOS binary + .app bundle with `codesign`**:
```bash
# Import cert into temporary keychain
KEYCHAIN="build.keychain-db"
KEYCHAIN_PASSWORD="actions"
security create-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN"
security set-keychain-settings -lut 21600 "$KEYCHAIN"
security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN"
echo "$CERT_BASE64" | base64 -d > /tmp/cert.pfx
security import /tmp/cert.pfx -k "$KEYCHAIN" -P "$CERT_PASSWORD" -T /usr/bin/codesign
security list-keychains -d user -s "$KEYCHAIN" login.keychain-db
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KEYCHAIN_PASSWORD" "$KEYCHAIN"

# Sign standalone binary
codesign --force --sign "NoMercy Entertainment" "./output/$PROJECT_NAME-macos-x64"
# Sign .app bundle (deep signs all nested binaries)
codesign --force --deep --sign "NoMercy Entertainment" "./output/$PROJECT_NAME.app"
# Sign DMG
codesign --force --sign "NoMercy Entertainment" "./output/$PROJECT_NAME-macos-x64.dmg"

# Cleanup
security delete-keychain "$KEYCHAIN"
rm /tmp/cert.pfx
```

**Make signing conditional** — only sign when `signing-cert-base64` input is non-empty.

### 2. `.github/workflows/build-executables.yml` — Pass signing secrets to action

**Add secrets declaration:**
```yaml
secrets:
  CODE_SIGNING_CERT_BASE64:
    required: false
  CODE_SIGNING_CERT_PASSWORD:
    required: false
```

**Pass to composite action:**
```yaml
- name: Build ${{ matrix.project }}
  uses: ./.github/actions/build-dotnet-project
  with:
    project: ${{ matrix.project }}
    dotnet-version: ${{ inputs.dotnet-version }}
    signing-cert-base64: ${{ secrets.CODE_SIGNING_CERT_BASE64 }}
    signing-cert-password: ${{ secrets.CODE_SIGNING_CERT_PASSWORD }}
```

### 3. `.github/workflows/ci-cd-pipeline.yml` — Pass secrets to build_executables

Add `secrets: inherit` to the `build_executables` job (currently missing):
```yaml
build_executables:
  needs: version_management
  if: needs.version_management.outputs.should_deploy == 'true'
  uses: ./.github/workflows/build-executables.yml
  secrets: inherit
  with:
    dotnet-version: "10.0.x"
    version: ${{ needs.version_management.outputs.version }}
```

### 4. `.github/actions/build-windows-installer/action.yml` — Sign Windows installer

**Add inputs:**
- `signing-cert-base64` (optional)
- `signing-cert-password` (optional)

**Add steps** (after payload build, before Inno Setup):

**a) Sign all .exe files in the payload:**
```powershell
$certBytes = [Convert]::FromBase64String($env:CERT_BASE64)
[IO.File]::WriteAllBytes("$env:TEMP\cert.pfx", $certBytes)

$exes = Get-ChildItem ./installer-payload -Filter "*.exe"
foreach ($exe in $exes) {
    & signtool.exe sign /f "$env:TEMP\cert.pfx" /p "$env:CERT_PASSWORD" `
        /d "NoMercy MediaServer" /du "https://nomercy.tv" `
        /fd sha256 /tr http://timestamp.digicert.com /td sha256 `
        $exe.FullName
}
```

**b) Configure Inno Setup SignTool via `/S` parameter and sign final installer**

### 5. `src/NoMercy.Service/NoMercyMediaServer.iss` — Add SignTool directive

Add `SignTool=MySignTool` to `[Setup]` section.

### 6. `.github/actions/build-macos-installer/action.yml` — Sign macOS installer

Import cert to keychain, sign payload binaries with `codesign`, pass `--sign-identity` to `build-pkg.sh`.

### 7. `packaging/macos/build-pkg.sh` — Add optional .pkg signing

Add `--sign-identity` CLI option. Sign .app bundle before `pkgbuild`, sign .pkg with `productsign` after `productbuild`.

### 8. `.github/workflows/build-packages.yml` — Pass signing secrets

Add `CODE_SIGNING_CERT_BASE64` and `CODE_SIGNING_CERT_PASSWORD` to secrets declaration, pass to composite actions.

## Certificate Provider Options

### macOS — Apple Developer Program: **$99/year**
- Includes unlimited code signing + notarization
- Eliminates Gatekeeper warnings entirely
- Best value by far for macOS

### Windows — OV Code Signing: **~$65-130/year**

| Provider | Price | Notes |
|----------|-------|-------|
| **Certum** (Open Source) | ~$50/yr | Only if project qualifies as open source |
| **Certum** (OV Cloud) | ~$108/yr | Cheapest standard option, cloud-based signing |
| **GoGetSSL** (reseller) | ~$72/yr | Resells Sectigo/Comodo certs |
| **CompareCheapSSL** | ~$64/yr | Reseller pricing |

### Windows — EV Code Signing: **~$250-350/yr**
- Instant SmartScreen reputation (no "unknown publisher" warning period)
- Certum EV Cloud: ~$290/yr

### Important notes
- Since June 2023, all OV/EV code signing certs **require HSM or cloud signing** — no plain PFX files anymore
- Certum's cloud option handles this (they host the key, you sign via their API/tool)
- From March 2026, certificate validity shortens from 39 months to 460 days

### Recommendation
Since NoMercy MediaServer is open source on GitHub:
1. **Certum Open Source Code Signing** (~$50/yr) for Windows
2. **Apple Developer Program** ($99/yr) for macOS
3. Total: ~$150/yr for both platforms with full OS trust

The self-signed approach (free) works as a starting point — the CI pipeline changes are the same either way, just swap the certificate later.

## Key Design Decisions

1. **Single certificate for both platforms.** One PFX/P12 file works with `osslsigncode`/`signtool` (Windows) and `codesign`/`productsign` (macOS).
2. **Signing is optional.** All signing steps are conditional on secrets being present.
3. **Timestamping with DigiCert.** Signatures remain valid after certificate expires.
4. **Temporary keychain on macOS.** Standard CI pattern for code signing.
5. **Self-signed = same trust model as Android.** OS warnings remain, but signature proves origin.

## Verification

1. Generate certificate locally and add secrets to GitHub
2. Push to dev to trigger CI
3. Check Windows: Right-click .exe → Properties → Digital Signatures → "NoMercy Entertainment"
4. Check macOS: `codesign -v --verbose` and `pkgutil --check-signature`
5. Without secrets: Verify builds still succeed (graceful skip)
