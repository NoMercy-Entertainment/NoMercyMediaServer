> [!CAUTION]
> :warning: **WORK IN PROGRESS - USE WITH CAUTION** :warning: <br>
> **This repository is under active development and currently not stable.**

<img src="https://raw.githubusercontent.com/NoMercy-Entertainment/NoMercyMediaServer/master/assets/icons/logo.png" style="width: auto;height: 240px;">

**"Empower Ownership: Overcome Licensing Barriers and Effortlessly Archive Your Media Collection with Privacy and Simplicity."**

[![CI/CD Pipeline](https://github.com/NoMercy-Entertainment/NoMercyMediaServer/workflows/CI%2FCD%20Pipeline/badge.svg)](https://github.com/NoMercy-Entertainment/NoMercyMediaServer/actions)

## Features

- **Automatic Encoding**: Effortlessly convert your media files into various formats.
- **Comprehensive Media Management**: Organize and curate your library with ease.
- **Remote Streaming**: Access your media collection from anywhere, just like popular streaming platforms.
- **User-Friendly Interface**: Enjoy a sleek, intuitive design that’s easy to navigate.

## Server Switching

NoMercy MediaServer also features seamless server switching within the user interface, providing:

- **Separate Watch Histories**: Maintain distinct watch histories for each server, keeping your viewing data private and organized.
- **Enhanced Privacy**: Manage your media experiences independently across different servers without any data crossover.
- **Greater Flexibility**: Easily switch between servers to explore different content libraries or maintain separate user profiles.

This feature guarantees a tailored, private, and flexible media experience, perfectly suited to your needs.

## Secure Connection & Port Configuration

**NoMercy MediaServer** provides a fully trusted SSL certificate to ensure a secure connection for both internal and external access to your media server.

## Account Requirement

A user account is **mandatory** for using NoMercy MediaServer. This account is essential for:

- **Privacy and Security**: Protect your data and maintain privacy while enabling personalized features.
- **Custom Playlists**: Create, share, and enjoy personalized playlists with friends and the community.
- **Exclusive Content**: Access special offers, community plugins, and user-generated content.
- **Social Interaction**: Connect with others, share recommendations, and be part of the NoMercy community.

**Important:** While an account is required to access the full range of features, your media files will **never** be locked behind this account or any form of DRM. They will always remain freely available from the storage device on which they are stored. We are committed to ensuring that your files are accessible at all times, regardless of your account status.

We have many more exciting features planned for the future, enhancing your experience even further!

## Installation

### Option 1: Download Pre-built Release (Recommended)

Download the latest release for your platform from the [Releases page](https://github.com/NoMercy-Entertainment/NoMercyMediaServer/releases).

| Platform | File |
|----------|------|
| Windows | `NoMercyMediaServer-windows-x64.exe` |
| Linux | `NoMercyMediaServer-linux-x64` |
| macOS | `NoMercyMediaServer-macos-x64.tar.gz` |

> [!NOTE]
> Pre-built releases are **self-contained** and do not require .NET to be installed.

**Linux/macOS**: Make the binary executable before running:
```bash
chmod +x NoMercyMediaServer-linux-x64
./NoMercyMediaServer-linux-x64
```

### Option 2: Linux Package Repository

Linux users can install directly from our package repository at [repo.nomercy.tv](https://repo.nomercy.tv).

<details>
<summary><strong>Debian / Ubuntu</strong></summary>

```bash
# Add repository key
wget -O - https://repo.nomercy.tv/nomercy_repo.gpg.pub | sudo gpg --dearmor -o /etc/apt/keyrings/nomercy-archive-keyring.gpg

# Add repository
echo "deb [signed-by=/etc/apt/keyrings/nomercy-archive-keyring.gpg] https://repo.nomercy.tv/apt stable main" | sudo tee /etc/apt/sources.list.d/nomercy.list

# Install
sudo apt update && sudo apt install nomercymediaserver
```
</details>

<details>
<summary><strong>Fedora / RHEL / CentOS</strong></summary>

```bash
# Add repository
sudo dnf config-manager --add-repo https://repo.nomercy.tv/rpm/nomercy.repo

# Import GPG key
sudo rpm --import https://repo.nomercy.tv/nomercy_repo.gpg.pub

# Install
sudo dnf install nomercymediaserver
```
</details>

<details>
<summary><strong>Arch Linux</strong></summary>

```bash
# Add repository
echo -e "Server = https://repo.nomercy.tv/arch-packages/pool/x86_64" | sudo tee /etc/pacman.d/nomercy-mirrorlist
echo -e "\n[nomercy]\nInclude = /etc/pacman.d/nomercy-mirrorlist" | sudo tee -a /etc/pacman.conf

# Add GPG key
curl -o nomercy.gpg.pub https://repo.nomercy.tv/nomercy_repo.gpg.pub
sudo pacman-key --add nomercy.gpg.pub
sudo pacman-key --lsign-key B8CE23865511524D442F7DCF9A4B71002C09D6B8

# Install
sudo pacman -Sy nomercymediaserver
```
</details>

### Option 3: Docker

Run NoMercy MediaServer in a container:

```bash
# CPU only
docker compose up -d

# With NVIDIA GPU
docker compose -f docker-compose.nvidia.yml up -d

# With Intel Quick Sync
docker compose -f docker-compose.intel.yml up -d

# With AMD GPU
docker compose -f docker-compose.amd.yml up -d
```

> [!IMPORTANT]
> Edit the compose file to update the media volume paths before running.

### Option 4: Build from Source

> [!TIP]
> Building from source requires the [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).

```bash
# Clone the repository
git clone https://github.com/NoMercy-Entertainment/NoMercyMediaServer.git
cd NoMercyMediaServer

# Restore dependencies and build
dotnet restore
dotnet build

# Run the server
dotnet run --project src/NoMercy.Service
```

## Usage

1. **Start the server** using one of the installation methods above.
2. **Authenticate**: On desktop, a browser window opens for login. On headless servers, enter credentials in the terminal.
3. **Access the web interface** at [https://app.nomercy.tv](https://app.nomercy.tv)
4. **Add your media** and start streaming!

### External Access & Port Forwarding

To access your server from outside your home network, you need to forward the port `7626` on your router. This will enable remote connections while maintaining a secure environment.

### Custom Port Configuration

You can customize the internal and external ports by using the following options when launching the server:

- **Internal Port**: `--internal-port=<number>`
- **External Port**: `--external-port=<number>`

These settings allow for flexible networking configurations, ensuring that the NoMercy MediaServer fits seamlessly into your existing network setup.

## Contact

For further information or support, visit NoMercy.tv or contact our support team.

Made with ❤️ by [NoMercy Entertainment](https://nomercy.tv)

Here's a little sneak peak 😋

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

Here's a neater version of your section, maintaining the playful tone while organizing the information for clarity:

### Screensaver 😍 (Suck it, Netflix! Mine's prettier)

![nm_screensaver_1](https://github.com/user-attachments/assets/ee4127af-ca65-43fa-8d5f-d7373e4e2479)

The sleek UI you see was designed by [@aaoa-dev](https://github.com/aaoa-dev), a designer with 16 years of experience. Want to see how it's done? AA streams the design process live on [YouTube](https://www.youtube.com/@aaoa_streams) and [Twitch](https://twitch.tv/aaoa_). You'll often find me there too, so feel free to drop by and follow the journey. If YouTube is your platform of choice, just give me a shout in the chat! 😊

### Want the icons and design kit you see here?

You can grab them [here](https://aaoa.lemonsqueezy.com) (I might have had a bit too much fun... my bad!)

![image](https://github.com/user-attachments/assets/edb55891-5acb-4a68-a860-2c54c394e609)

### Special Thanks 💖

A **huge** thank you to my friend Mark, who made all of this possible by generously donating the hosting and domain for the entire duration of this project. Words can't express how grateful I am for your support!

I also want to extend my heartfelt thanks to my friend, [Fill84](https://github.com/Fill84), for his invaluable help with the programming. Your contributions have been crucial, and I'm deeply appreciative!

## Repository Note

> This repository was recreated on January 2026 due to repository bloat from 
> accidentally committed large files. The previous repository with full history 
> is archived at [NoMercyMediaServer-archive](https://github.com/NoMercy-Entertainment/NoMercyMediaServer-archive).