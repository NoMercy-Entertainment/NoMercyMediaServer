#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# Starts the four storage backends (MinIO/S3, Apache/WebDAV, kernel nfsd/NFS,
# Samba/SMB) over a shared /data root and keeps the container alive. The fixture
# waits on each port before running tests; readiness here is best-effort logging.
# -----------------------------------------------------------------------------
set -u

WEBDAV_USER="${WEBDAV_USER:-testuser}"
WEBDAV_PASS="${WEBDAV_PASS:-testpass}"
SMB_USER="${SMB_USER:-testuser}"
SMB_PASS="${SMB_PASS:-testpass}"

mkdir -p /data/nfs /data/webdav /data/minio /data/smb /media
chmod -R 0777 /data

# Seed the NAS-shaped layout BEFORE nfsd exports it. Generic names that still
# exercise the real-world path edge cases the live-NAS tests cover: a literal
# '$', square brackets, spaces, and a UTF-8 character. The live-NAS tests assert
# on exactly this layout.
seed_nfs() {
    local base="/data/nfs/Music/A/artist\$/[2025] album\$"
    mkdir -p "$base"
    printf 'ID3-track-one' > "$base/01 track one.mp3"
    printf 'ID3-track-two' > "$base/02 track two [feat. Tëst].mp3"
    local d
    for d in A B C D E; do mkdir -p "/data/nfs/Music/$d"; done
    chmod -R 0777 /data/nfs
}
seed_nfs

# --- MinIO (S3) on :9000 -----------------------------------------------------
export MINIO_ROOT_USER="${MINIO_ROOT_USER:-minioadmin}"
export MINIO_ROOT_PASSWORD="${MINIO_ROOT_PASSWORD:-minioadmin}"
minio server /data/minio --address ":9000" >/var/log/minio.log 2>&1 &

# --- Apache (WebDAV) on :80, Basic auth --------------------------------------
htpasswd -bc /etc/apache2/webdav.htpasswd "$WEBDAV_USER" "$WEBDAV_PASS" >/dev/null 2>&1
mkdir -p /var/lib/dav
chown -R www-data:www-data /var/lib/dav /data/webdav
export APACHE_RUN_USER=www-data
export APACHE_RUN_GROUP=www-data
export APACHE_PID_FILE=/var/run/apache2/apache2.pid
export APACHE_RUN_DIR=/var/run/apache2
export APACHE_LOCK_DIR=/var/lock/apache2
export APACHE_LOG_DIR=/var/log/apache2
mkdir -p "$APACHE_RUN_DIR" "$APACHE_LOCK_DIR" "$APACHE_LOG_DIR"
apache2ctl start

# --- Kernel NFS server (NFSv3 + NFSv4) on :2049 ------------------------------
# Real in-kernel nfsd via the Docker host's nfsd module — needs --privileged
# (the fixture sets it). rpcbind + mountd give libnfs the v3 MOUNT protocol;
# v4 is served on 2049 directly. fsid=0 makes /data/nfs the v4 root.
mkdir -p /run/sendsigs.omit.d /var/lib/nfs/rpc_pipefs /proc/fs/nfsd
mount -t rpc_pipefs rpc_pipefs /var/lib/nfs/rpc_pipefs 2>/dev/null || true
mount -t nfsd nfsd /proc/fs/nfsd 2>/dev/null || true
# Shrink the NFSv4 grace/lease so the export is writable almost immediately —
# a throwaway single-client test server has no reboot recovery to protect, and
# the default ~90s grace makes the first write fail with NFS4ERR_GRACE.
echo 10 > /proc/fs/nfsd/nfsv4gracetime 2>/dev/null || true
echo 10 > /proc/fs/nfsd/nfsv4leasetime 2>/dev/null || true
rpcbind -w 2>/dev/null || rpcbind 2>/dev/null || true
exportfs -r 2>/dev/null || true
rpc.nfsd --no-udp 8 2>/dev/null || rpc.nfsd 8 2>/dev/null || true
# Pin mountd to a fixed port so it can be published through Docker — otherwise
# libnfs's v3 MOUNT call gets a random high port from rpcbind that isn't mapped
# (POLLHUP). 20048 is the conventional fixed mountd port.
rpc.mountd --no-udp --port 20048 2>/dev/null || true
exportfs -a 2>/dev/null || true

# --- Samba (SMB) ------------------------------------------------------------
# Provision the standalone domain before adding the user. Without a domain SID
# the user's group-SID lookup fails ("SID ...-514 belongs to our domain, but
# there is no corresponding object") and every login returns LOGON_FAILURE even
# though the password is correct.
mkdir -p /var/lib/samba/private /var/cache/samba /run/samba /var/log/samba
# Fixed domain SID so the standalone domain is fully resolvable.
net setdomainsid S-1-5-21-1111111111-2222222222-3333333333 2>/dev/null || true
# Create the Unix user + its own primary group so Samba can map the group SID.
groupadd -f "$SMB_USER" 2>/dev/null || true
id "$SMB_USER" >/dev/null 2>&1 \
    || useradd -M -s /usr/sbin/nologin -g "$SMB_USER" "$SMB_USER" 2>/dev/null || true
printf '%s\n%s\n' "$SMB_PASS" "$SMB_PASS" | smbpasswd -s -a "$SMB_USER" >/dev/null 2>&1 || true
smbpasswd -e "$SMB_USER" >/dev/null 2>&1 || true
smbd --daemon --no-process-group >/var/log/smbd.log 2>&1 || smbd -D >/var/log/smbd.log 2>&1 || true

echo "storage-backends container started (s3:9000 webdav:8080 nfs:2049 smb:1445)"

# Keep the container in the foreground.
tail -f /dev/null
