#!/bin/bash
# Copyright (c) 2026 Phantom.Relay. All rights reserved.
#
# Builds the two FFmpeg libraries Nightcast's Linux H.264 decoder loads (libavcodec, libavutil), as a minimal LGPL
# shared build: the h264 decoder and parser and the VA-API hwaccel, nothing else. Run by build-ffmpeg-linux.ps1 inside
# WSL (Ubuntu 22.04: glibc 2.35, older than SteamOS's, so the libraries run there); any Debian-like Linux works too.
#
#   build-ffmpeg-linux.sh <output folder>
#
# The source is the pinned release tarball from ffmpeg.org, checked against its SHA-256 before it is unpacked; nothing
# of FFmpeg is ever kept in the repository. The build happens under /tmp with a neutral prefix, so no path of the
# building machine ends up in the libraries (avcodec_configuration() carries the configure line). libva is linked
# dynamically: the system's libva.so.2 and libva-drm.so.2 (SteamOS ships both, with Mesa's radeonsi VA driver).
set -euo pipefail

FFMPEG_VERSION="8.1.3"
FFMPEG_SHA256="7138d28c96d9d3e3af4ee3d8cad72741f8ffb40da90c1112235dea3ecd3178a3"
FFMPEG_URL="https://ffmpeg.org/releases/ffmpeg-${FFMPEG_VERSION}.tar.xz"
WORK="/tmp/nightcast-ffmpeg-${FFMPEG_VERSION}"
PREFIX="/opt/nightcast-ffmpeg"

OUTPUT="${1:?usage: build-ffmpeg-linux.sh <output folder>}"

# What the build needs: a compiler, nasm for the x86 assembly, pkg-config and libva's headers.
for tool in cc make nasm pkg-config curl sha256sum; do
    command -v "$tool" >/dev/null || { echo "missing: $tool (apt-get install build-essential nasm pkg-config curl)" >&2; exit 1; }
done

pkg-config --exists libva libva-drm || { echo "missing: libva's headers (apt-get install libva-dev)" >&2; exit 1; }

mkdir -p "$WORK"
cd "$WORK"

if [ ! -f "ffmpeg-${FFMPEG_VERSION}.tar.xz" ]; then
    curl -sSfL "$FFMPEG_URL" -o "ffmpeg-${FFMPEG_VERSION}.tar.xz"
fi

echo "${FFMPEG_SHA256}  ffmpeg-${FFMPEG_VERSION}.tar.xz" | sha256sum -c --quiet - || { echo "the tarball's SHA-256 is not the pinned one" >&2; exit 1; }

rm -rf "ffmpeg-${FFMPEG_VERSION}" stage
tar -xf "ffmpeg-${FFMPEG_VERSION}.tar.xz"
cd "ffmpeg-${FFMPEG_VERSION}"

# LGPL (no --enable-gpl, no --enable-nonfree); only libavcodec and libavutil.
./configure \
    --prefix="$PREFIX" \
    --enable-shared --disable-static --enable-pic \
    --disable-everything --disable-autodetect \
    --disable-programs --disable-doc --disable-network --disable-debug \
    --disable-avdevice --disable-avformat --disable-avfilter --disable-swscale --disable-swresample \
    --enable-vaapi \
    --enable-decoder=h264 --enable-parser=h264 --enable-hwaccel=h264_vaapi

# The build must be LGPL and must have VA-API with its DRM display (a render node, as on SteamOS).
grep -q '^#define CONFIG_GPL 0' config.h || { echo "the build is not LGPL" >&2; exit 1; }
grep -q '^#define CONFIG_NONFREE 0' config.h || { echo "the build is nonfree" >&2; exit 1; }
grep -q '^#define CONFIG_H264_DECODER 1' config_components.h || { echo "the h264 decoder is not in the build" >&2; exit 1; }
grep -q '^#define CONFIG_H264_VAAPI_HWACCEL 1' config_components.h || { echo "the h264 VA-API hwaccel is not in the build" >&2; exit 1; }
grep -q '^#define HAVE_VAAPI_DRM 1' config.h || { echo "VA-API's DRM display is not in the build" >&2; exit 1; }

# libavcodec finds libavutil beside itself ($ORIGIN), wherever the app unpacks them. Given to make, not configure,
# which would expand the dollar; FFmpeg's makefiles expand the flags twice ($$$$ to $$ to $), and the backslash keeps the shell off it.
make -j"$(nproc)" LDSOFLAGS='-Wl,-rpath,\$$$$ORIGIN'
make install DESTDIR="$WORK/stage" LDSOFLAGS='-Wl,-rpath,\$$$$ORIGIN'
readelf -d libavcodec/libavcodec.so | grep -q 'RUNPATH.*\[\$ORIGIN\]' || { echo 'libavcodec has no $ORIGIN run path' >&2; exit 1; }

mkdir -p "$OUTPUT"
LIB="$WORK/stage$PREFIX/lib"
cp -L "$LIB"/libavcodec.so.[0-9][0-9] "$LIB"/libavutil.so.[0-9][0-9] "$OUTPUT"/
cp LICENSE.md COPYING.LGPLv2.1 "$OUTPUT"/
# The headers of this build, for anyone checking the struct offsets the decoder mirrors (docs/architecture/linux-decode.md).
rm -rf "$OUTPUT/include"
cp -r "$WORK/stage$PREFIX/include" "$OUTPUT/include"

{
    echo "FFmpeg ${FFMPEG_VERSION} (${FFMPEG_URL}, sha256 ${FFMPEG_SHA256})"
    echo "License: LGPL v2.1 or later; dynamically linked, replaceable by the user."
    echo "Configure: $(grep FFMPEG_CONFIGURATION config.h | cut -d'"' -f2)"
    echo "Newest glibc symbol needed: $(objdump -T "$OUTPUT"/lib*.so.* | grep -o 'GLIBC_[0-9.]*' | sort -uV | tail -1)"
} > "$OUTPUT/FFMPEG-BUILD.txt"

cat "$OUTPUT/FFMPEG-BUILD.txt"
ls -la "$OUTPUT"
