> [!CAUTION]
> **Work in progress — use with caution.** This repository is under active development and not yet stable.

<p align="center">
<img src="https://raw.githubusercontent.com/NoMercy-Entertainment/nomercy-media-server/master/assets/icons/logo.png" style="width: auto;height: 240px;">
</p>

<h1 align="center">NoMercy MediaServer</h1>

<p align="center">
  <strong>The Effortless Encoder.</strong><br/>
  Making owning movies, shows, and music just as easy as using a streaming service.
</p>

<p align="center">
  <a href="https://github.com/NoMercy-Entertainment/nomercy-media-server/actions">
    <img src="https://github.com/NoMercy-Entertainment/nomercy-media-server/workflows/CI%2FCD%20Pipeline/badge.svg" alt="CI/CD Pipeline" />
  </a>
</p>

---

## Features

- **Automatic Encoding** — Convert your media files into various formats effortlessly
- **Media Management** — Organize and curate your library with rich metadata
- **Remote Streaming** — Access your collection from anywhere via [app.nomercy.tv](https://app.nomercy.tv)
- **Server Switching** — Separate watch histories and profiles across multiple servers
- **Trusted SSL** — Built-in certificate for secure internal and external access
- **No DRM** — Your files always stay on your storage, accessible regardless of account status

## Screenshots

<details>
<summary>Click to expand</summary>

### Home
![nm_home_1](https://github.com/user-attachments/assets/cce49509-c0be-48c1-83f1-d080d9d16337)

### Info
![nm_info_1](https://github.com/user-attachments/assets/d94716e5-aa0f-4b84-a2de-28f99949d7d6)
![nm_info_2](https://github.com/user-attachments/assets/92ae3883-f26c-4f95-bed9-0c6aced12b27)
![nm_info_3](https://github.com/user-attachments/assets/cc88f176-9b32-467e-b7e1-ee31635853b3)

### Custom Video and Trailer player
![nm_trailer](https://github.com/user-attachments/assets/47af9eaa-6303-4f98-8e88-c80ce5803225)

### Image Viewer
![nm_image_viewer](https://github.com/user-attachments/assets/1d8bbf19-2a26-46e8-b38f-d5d89ffae239)

### Video Library
![nm_movies](https://github.com/user-attachments/assets/c9088bce-7d19-48d0-b012-ce5ec18d77bc)

### Movie Collection
![nm_collection_1](https://github.com/user-attachments/assets/18c5bbd6-8987-4914-a74e-d7f1a80ab9e3)

### Person
![nm_person_1](https://github.com/user-attachments/assets/d9d5b105-b1d4-4854-aded-c930489e5526)

### Music
![nm_music_albums](https://github.com/user-attachments/assets/f97a81cf-8062-4383-a2b7-a8f46b18bd4d)
![nm_music_artist_1](https://github.com/user-attachments/assets/ac37fe32-eb1b-4fff-a8a6-9af87eb1ac81)
![nm_artist_albums_1](https://github.com/user-attachments/assets/061bdbfb-25d8-436e-a65f-e5459e719a98)

![nm_music_playing](https://github.com/user-attachments/assets/bc722c14-6b23-4eb3-a784-89bdffa8cd66)

### Screensaver 😍 (Suck it, Netflix! Mine's prettier)

![nm_screensaver_1](https://github.com/user-attachments/assets/ee4127af-ca65-43fa-8d5f-d7373e4e2479)

</details>

---

## Quick Start

**1. Download the installer for your platform:**

| Platform | File |
|:--|:--|
| Windows | `NoMercyMediaServer-windows-x64.exe` |
| Linux | `NoMercyMediaServer-linux-x64` |
| macOS | `NoMercyMediaServer-macos-x64.tar.gz` |

> [!NOTE]
> Installers and standalone binaries are self-contained — no .NET installation required.
> Find all downloads on the [Releases page](https://github.com/NoMercy-Entertainment/nomercy-media-server/releases/latest).

**2. Run the server and log in.**
On desktop, a browser window opens automatically for authentication. On headless servers, navigate to `http://<your-server-ip>:7626/setup` to complete setup.

**3. Open [app.nomercy.tv](https://app.nomercy.tv), add your media, and start streaming.**

That's it — you're up and running.

---

## Installation Options

### Linux Package Repository

Install from [repo.nomercy.tv](https://repo.nomercy.tv):

<details>
<summary>Debian / Ubuntu</summary>

```bash
wget -O - https://repo.nomercy.tv/nomercy_repo.gpg.pub | sudo gpg --dearmor -o /etc/apt/keyrings/nomercy-archive-keyring.gpg
echo "deb [signed-by=/etc/apt/keyrings/nomercy-archive-keyring.gpg] https://repo.nomercy.tv/apt stable main" | sudo tee /etc/apt/sources.list.d/nomercy.list
sudo apt update && sudo apt install nomercy
```
</details>

<details>
<summary>Fedora / RHEL / CentOS</summary>

```bash
sudo dnf config-manager --add-repo https://repo.nomercy.tv/rpm/nomercy.repo
sudo rpm --import https://repo.nomercy.tv/nomercy_repo.gpg.pub
sudo dnf install nomercy
```
</details>

<details>
<summary>Arch Linux</summary>

```bash
echo -e "Server = https://repo.nomercy.tv/arch-packages/pool/x86_64" | sudo tee /etc/pacman.d/nomercy-mirrorlist
echo -e "\n[nomercy]\nInclude = /etc/pacman.d/nomercy-mirrorlist" | sudo tee -a /etc/pacman.conf
curl -o nomercy.gpg.pub https://repo.nomercy.tv/nomercy_repo.gpg.pub
sudo pacman-key --add nomercy.gpg.pub
sudo pacman-key --lsign-key B8CE23865511524D442F7DCF9A4B71002C09D6B8
sudo pacman -Sy nomercy
```
</details>

### Docker

```bash
# Set your host LAN IP (required for container ↔ host communication)
export HOST_IP=$(hostname -I | awk '{print $1}')

# Pull and run — pick your variant:
docker compose up -d                                    # CPU only
docker compose -f docker-compose.nvidia.yml up -d       # NVIDIA GPU
docker compose -f docker-compose.intel.yml up -d        # Intel Quick Sync
docker compose -f docker-compose.amd.yml up -d          # AMD GPU
```

You can also pull images directly from the container registry:

```bash
docker pull ghcr.io/nomercy-entertainment/nomercymediaserver:latest    # CPU
docker pull ghcr.io/nomercy-entertainment/nomercymediaserver:nvidia    # NVIDIA
docker pull ghcr.io/nomercy-entertainment/nomercymediaserver:intel     # Intel
docker pull ghcr.io/nomercy-entertainment/nomercymediaserver:amd       # AMD
```

> [!IMPORTANT]
> Export `HOST_IP` before running — the compose files use it for `NOMERCY_INTERNAL_IP`. Edit the compose file to update media volume paths.

### Build from Source

Requires the [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

```bash
git clone https://github.com/NoMercy-Entertainment/nomercy-media-server.git
cd nomercy-media-server
dotnet restore && dotnet build
dotnet run --project src/NoMercy.Service
```

---

## Configuration

### Port Forwarding

Forward port `7626` on your router to access your server from outside your home network.

### Startup Flags

| Flag | Short | Description |
|:--|:--|:--|
| `--loglevel` | `-l` | Log level (`Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`) |
| `--seed` | | Seed database with sample data |
| `--internal-port` | `-i` | Internal port (default: `7626`) |
| `--external-port` | `-x` | External port (default: `7626`) |
| `--internal-ip` | | Internal IP address override |
| `--external-ip` | | External IP address override |
| `--pipe-name` | | Named pipe (Windows) or Unix socket name for IPC |
| `--service` | | Run as a platform service (Windows SCM, systemd, launchd) |

---

## Credits

UI designed by [**AAOA**](https://github.com/aaoa-dev) — catch the design process live on [YouTube](https://www.youtube.com/@aaoa_streams) and [Twitch](https://twitch.tv/aaoa_). Icons and design kit available at [aaoa.lemonsqueezy.com](https://aaoa.lemonsqueezy.com).

Special thanks to **Mark** for donating hosting and the domain, and to [**Fill84**](https://github.com/Fill84) for programming contributions.

## Repository Note

> This repository was recreated in January 2026 due to repository bloat. The previous repo with full history is archived at [NoMercyMediaServer-archive](https://github.com/NoMercy-Entertainment/nomercy-media-server-archive).

## Contact

[NoMercy.tv](https://nomercy.tv) · [Discord](https://discord.gg/dHHvnvFsXR)

---

<p align="center">Made with ❤️ by <a href="https://nomercy.tv">NoMercy Entertainment</a></p>
