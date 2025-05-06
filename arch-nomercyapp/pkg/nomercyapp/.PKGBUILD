pkgname = nomercyapp
pkgver = 0.1.96-1 # Arch package version format: pkgver-pkgrel
pkgdesc = NoMercy-App
url = https://nomercy.tv
builddate = 1746539239
size = 48461246 # Size of the binary
arch = x86_64
license = custom # Or the actual license
depend = glibc # Basic dependency, add others if needed
# No source or build steps needed as we are packaging pre-built binaries

package() {
  # Install binary, desktop file, and icon into the package root ()
  install -Dm755 ./output/NoMercyApp ${pkgdir}/usr/bin/nomercyapp
  install -Dm644 ./src/NoMercy.Server/Assets/Linux/NoMercy-App.desktop ${pkgdir}/usr/share/applications/
  install -Dm644 ./src/NoMercy.Server/Assets/Linux/icon.png ${pkgdir}/usr/share/icons/hicolor/scalable/apps/NoMercy-App.png
}
