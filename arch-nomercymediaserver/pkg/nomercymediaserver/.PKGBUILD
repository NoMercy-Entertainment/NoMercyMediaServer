pkgname = nomercymediaserver
pkgver = 0.1.96-1 # Arch package version format: pkgver-pkgrel
pkgdesc = NoMercy-MediaServer
url = https://nomercy.tv
builddate = 1746539218
size = 79051784 # Size of the binary
arch = x86_64
license = custom # Or the actual license
depend = glibc # Basic dependency, add others if needed
# No source or build steps needed as we are packaging pre-built binaries

package() {
  # Install binary, desktop file, and icon into the package root ()
  install -Dm755 ./output/NoMercyMediaServer ${pkgdir}/usr/bin/nomercymediaserver
  install -Dm644 ./src/NoMercy.Server/Assets/Linux/NoMercy-MediaServer.desktop ${pkgdir}/usr/share/applications/
  install -Dm644 ./src/NoMercy.Server/Assets/Linux/icon.png ${pkgdir}/usr/share/icons/hicolor/scalable/apps/NoMercy-MediaServer.png
}
