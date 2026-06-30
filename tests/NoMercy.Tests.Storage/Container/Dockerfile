# -----------------------------------------------------------------------------
#  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
#
#  This file is part of NoMercy MediaServer, source-available software (NOT open
#  source). Personal use and contributions are welcome; distribution, resale,
#  relicensing, and commercial exploitation are prohibited without explicit
#  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
#
#  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
# -----------------------------------------------------------------------------
#
# One all-in-one backend image for the storage test suite. Serves three real
# remote-storage protocols from a single container over a shared /data root, so
# any dev machine runs the full suite identically with one deploy/teardown:
#
#   - MinIO            S3            :9000   (creds minioadmin/minioadmin)
#   - Apache mod_dav   WebDAV        :80     (root /data/webdav)
#   - NFS Ganesha      NFSv4         :2049   (export /data/nfs, userspace -> no
#                                             kernel modules, no --privileged)
#
# ffmpeg is included so the container can also generate the encoder test media
# (HDR10, SD, 1080p, audio-only) into /data/media on demand via `docker exec`.

FROM debian:bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl wget \
        apache2 apache2-utils \
        nfs-kernel-server nfs-common rpcbind \
        ffmpeg \
        netcat-openbsd \
    && rm -rf /var/lib/apt/lists/*

# MinIO server binary, pinned to a tagged release (not the bleeding-edge build
# at /release/.../minio which scout reports as +dirty). This is a throwaway,
# localhost-only test backend that never ships, so MinIO's own transitive Go
# CVEs are out of scope — pinning a tag just keeps the build reproducible.
ARG MINIO_RELEASE=minio.RELEASE.2025-04-22T22-12-26Z
RUN curl -fsSL "https://dl.min.io/server/minio/release/linux-amd64/archive/${MINIO_RELEASE}" \
        -o /usr/local/bin/minio \
    && chmod +x /usr/local/bin/minio

# Apache WebDAV vhost on :8080 (port 80 commonly collides with a host service
# under --network host) with mod_dav + Basic auth.
RUN a2enmod dav dav_fs auth_basic authn_file >/dev/null \
    && echo "Listen 8080" > /etc/apache2/ports.conf
COPY webdav.conf /etc/apache2/sites-available/000-default.conf

# Kernel NFS export of /data/nfs (NFSv3 + NFSv4) for all clients. all_squash +
# anonuid/anongid=0 maps every client identity to root, so a file written by one
# open-owner is always readable by the next — the libnfs client connects without
# uid/gid, and otherwise the read-back hits NFS4ERR_ACCESS.
RUN mkdir -p /data/nfs \
    && echo "/data/nfs *(rw,sync,no_subtree_check,all_squash,anonuid=0,anongid=0,insecure,fsid=0)" > /etc/exports

COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

EXPOSE 9000 80 2049

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
