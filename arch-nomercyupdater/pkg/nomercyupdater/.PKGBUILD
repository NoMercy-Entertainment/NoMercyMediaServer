pkgname = nomercyupdater
pkgver = 0.1.96-1 # Arch package version format: pkgver-pkgrel
pkgdesc = NoMercy-Updater
url = https://nomercy.tv
builddate = 1746539229
size = 79105899 # Size of the binary
arch = x86_64
license = custom # Or the actual license
depend = glibc # Basic dependency, add others if needed
# No source or build steps needed as we are packaging pre-built binaries

package() {
  # Install binary, desktop file, and icon into the package root ()
  install -Dm755 ./output/NoMercyUpdater ${pkgdir}/usr/bin/nomercyupdater
  install -Dm644 ./src/NoMercy.Server/Assets/Linux/NoMercy-Updater.desktop ${pkgdir}/usr/share/applications/
  install -Dm644 ./src/NoMercy.Server/Assets/Linux/icon.png ${pkgdir}/usr/share/icons/hicolor/scalable/apps/NoMercy-Updater.png
}
