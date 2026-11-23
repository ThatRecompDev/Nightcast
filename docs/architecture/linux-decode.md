# Hardware H.264 decoding on Linux (SteamOS)

On SteamOS (the ROG Ally: AMD Z1 Extreme, RDNA3, Mesa's radeonsi VA-API driver) the console's H.264 (Main 4.2, up to
2560 x 1440, 30 to 60 fps) is decoded on the GPU through VA-API. Step 1, built: FFmpeg's libavcodec with its VA-API
hwaccel, pictures read back to the CPU and drawn through the existing picture path. Step 2, designed only: the
pictures left on the GPU, imported into Dawn on Vulkan as DMA-BUFs.

## Step 1: libavcodec on VA-API, read back

| Piece | Where |
|---|---|
| The decoder | `src/Phantom.Relay.Media.Linux/VaapiH264Decoder.cs` (`IVideoDecoder`, `[SupportedOSPlatform("linux")]`) |
| FFmpeg's calls | `Interop/AvCodecMethods.cs`, `AvUtilMethods.cs`, `VaMethods.cs` (`[LibraryImport]`, NativeAOT-safe) |
| The mirrored struct fields | `Interop/FfmpegLayout.cs`, for FFmpeg 8 (libavcodec 62, libavutil 60) |
| The libraries | `scripts/build/build-ffmpeg-linux.ps1` (WSL) runs `build-ffmpeg-linux.sh` into `artifacts/native/linux-x64/ffmpeg` |
| Their way into the app | `src/Phantom.Relay.Media.Linux/FfmpegLibraries.targets`, imported by the desktop and the probe |
| The choice | `DesktopProgram.OpenDecoder`: on Linux VA-API first ("Decoding on the GPU (VA-API, <driver>)."), else openh264 |
| The probe | `src/Phantom.Relay.Tools.DecodeProbe`: a sample decoded with each picture's timing and its difference from a reference |

**How it decodes.** `TryOpen` loads libavutil then libavcodec (from the app's folder, else the system's), refuses any
major version but 62 and 60, makes FFmpeg's VA-API device on `/dev/dri/renderD128` (`av_hwdevice_ctx_create`) and asks
libva for the driver's name (`vaQueryVendorString`). The H.264 context gets the device (`hw_device_ctx`), a
`get_format` callback (`[UnmanagedCallersOnly]`, `FormatChoice`) that takes `AV_PIX_FMT_VAAPI`, `flags=+low_delay`,
one thread and no thread type, so each access unit's picture comes out of its own `avcodec_send_packet` /
`avcodec_receive_frame` (no reordering, no frame-thread delay). `TakePicture` reads the VA surface back with
`av_hwframe_transfer_data` into an NV12 frame and splits it into the `VideoFrame`'s planes (Media.Windows'
`Nv12Planes.Deinterleave`, each plane with its own pitch). When the hwaccel cannot take a stream (an unsupported
profile or size), libavcodec calls `get_format` again without `VAAPI`, the decoder takes 4:2:0 planes in software and
`IsHardwareAccelerated` turns false. Colour comes from the SPS's VUI, as on Windows (`H264StreamColour`).

**Timing.** As on Windows: `LastDecodeDuration` is the send and receive; VA-API decodes asynchronously, so the wait
for the GPU lands in `LastCopyDuration` (the transfer's `vaSyncSurface`, the read of the surface, the split). The decode
worker's own decode time includes both.

**Failures say why.** Missing libraries ("libavutil.so.60 is not there ..."), another FFmpeg major, a VA-API device that
does not start ("VA-API did not start on /dev/dri/renderD128: Input/output error (-5)"), a context that does not open.
The desktop then prints "No VA-API decoder: ..." and decodes with openh264.

### FFmpeg

| | |
|---|---|
| Version | FFmpeg 8.1.3 (libavcodec 62.28.103, libavutil 60.26.103) |
| Source | https://ffmpeg.org/releases/ffmpeg-8.1.3.tar.xz, sha256 `7138d28c96d9d3e3af4ee3d8cad72741f8ffb40da90c1112235dea3ecd3178a3`; its signature checked once against FFmpeg's release key (FCF9 86EA 15E6 E293 A564 4F10 B432 2F04 D676 58D8) when the hash was pinned; the script checks the hash on every build |
| Licence | LGPL v2.1 or later: no `--enable-gpl`, no `--enable-nonfree` (the script stops otherwise) |
| Configure | `--enable-shared --disable-static --enable-pic --disable-everything --disable-autodetect --disable-programs --disable-doc --disable-network --disable-debug --disable-avdevice --disable-avformat --disable-avfilter --disable-swscale --disable-swresample --enable-vaapi --enable-decoder=h264 --enable-parser=h264 --enable-hwaccel=h264_vaapi`, prefix `/opt/nightcast-ffmpeg` |
| Built on | WSL Ubuntu 22.04 (glibc 2.35, libva 2.12); the newest glibc symbol needed is GLIBC_2.35 (`hypot`), older than SteamOS's |
| Output | `libavcodec.so.62` (1.6 MB), `libavutil.so.60` (1.0 MB), both with RUNPATH `$ORIGIN` (libavcodec finds libavutil beside it), plus `LICENSE.md`, `COPYING.LGPLv2.1`, the headers and `FFMPEG-BUILD.txt` |
| Needs on the system | `libva.so.2`, `libva-drm.so.2`, `libc`, `libm`, and a VA driver (radeonsi on SteamOS: expected in the base image with Mesa, not checked on the Ally) |

The build runs under `/tmp` with a neutral prefix, so no path of the building machine is in the libraries (checked:
the only path-like string is FFmpeg's own `/tmp/%sXXXXXX` template). Nothing of FFmpeg is ever committed.

### The mirrored layout

Measured with this program against the build's headers (`artifacts/native/linux-x64/ffmpeg/include`):

```c
#include <stdio.h>
#include <stddef.h>
#include <libavcodec/avcodec.h>
#include <libavutil/hwcontext.h>
#include <libavutil/hwcontext_vaapi.h>

int main(void) {
    printf("get_format %zu hw_device_ctx %zu\n", offsetof(AVCodecContext, get_format), offsetof(AVCodecContext, hw_device_ctx));
    printf("pkt pts %zu data %zu size %zu\n", offsetof(AVPacket, pts), offsetof(AVPacket, data), offsetof(AVPacket, size));
    printf("frame data %zu linesize %zu width %zu height %zu format %zu pts %zu\n", offsetof(AVFrame, data),
           offsetof(AVFrame, linesize), offsetof(AVFrame, width), offsetof(AVFrame, height), offsetof(AVFrame, format), offsetof(AVFrame, pts));
    printf("bufref data %zu hwdevice hwctx %zu vaapi display %zu\n", offsetof(AVBufferRef, data),
           offsetof(AVHWDeviceContext, hwctx), offsetof(AVVAAPIDeviceContext, display));
    printf("H264 %d VAAPI dev %d pix VAAPI %d NV12 %d\n", AV_CODEC_ID_H264, AV_HWDEVICE_TYPE_VAAPI, AV_PIX_FMT_VAAPI, AV_PIX_FMT_NV12);
    return 0;
}
```

FFmpeg 8.1.3: `get_format` 192, `hw_device_ctx` 560; AVPacket `pts` 8, `data` 24, `size` 32; AVFrame `data` 0,
`linesize` 64, `width` 104, `height` 108, `format` 116, `pts` 136; AVBufferRef `data` 8; AVHWDeviceContext `hwctx` 16;
AVVAAPIDeviceContext `display` 0; AV_CODEC_ID_H264 27, AV_HWDEVICE_TYPE_VAAPI 3, AV_PIX_FMT_VAAPI 44, NV12 23,
YUV420P 0, YUVJ420P 12; AVERROR(EAGAIN) -11, AVERROR_EOF -541478725. FFmpeg moves or removes public fields only at a
major version bump, which is why the major is checked at start. A new FFmpeg major means running the program again
and updating `FfmpegLayout` and `FfmpegLibraries` together. The low-delay flag and the threads are set by name
(`av_opt_set`), not by offset.

### Measured

On the owner's PC, in WSL2 (Ubuntu 22.04), through Mesa 23.2.1's **d3d12** VA driver, which turns VA-API into D3D12
video calls on the Windows GPU through WSL's paravirtualized driver. That is not radeonsi on the Ally's VCN, so these
numbers show that the decoder works and how its parts compare, not what the Ally will measure. The machine was also
busy with the other lanes' builds. Samples: 120 frames of x264 at 60 fps, Main profile, level 4.2, no B-frames, 25
Mbit/s at most, an access unit delimiter before each picture, generated from FFmpeg's `testsrc2` with temporal noise
(never a capture of a real session). Each picture was compared with FFmpeg's software decode of the same file.

| GPU (via d3d12 VA) | Size | Decode avg / worst | Read-back avg / worst | First picture (decode, read-back) | Difference from the reference |
|---|---|---|---|---|---|
| AMD Radeon (the PC's integrated GPU) | 2560 x 1440 | 2.17 / 8.88 ms | 13.62 / 18.28 ms | 20.5, 30.2 ms | 0 (bit-exact) |
| AMD Radeon (the PC's integrated GPU) | 1920 x 1080 | 1.62 / 5.64 ms | 10.78 / 15.13 ms | 19.9, 15.9 ms | 0 |
| AMD Radeon (the PC's integrated GPU) | 1280 x 720 | 1.26 / 4.82 ms | 6.30 / 11.17 ms | 19.1, 9.2 ms | 0 |
| NVIDIA RTX 4090 | 2560 x 1440 | 7.76 / 33.89 ms | 4.88 / 9.13 ms | 80.9, 17.9 ms | 0 |

A single-file build of the probe (the libraries inside, unpacked on first start) gave the same result at 1440p on
the AMD GPU: 2.57 ms decode, 18.35 ms read-back, bit-exact. The "first" picture also makes the decoder's surfaces.
"Read-back" is `av_hwframe_transfer_data` (which waits for the GPU) and the NV12 split. In WSL it dominates: VA-API's
image mapping goes through the paravirtualized D3D12 device. On the Ally the read-back is from GPU memory mapped
write-combined or uncached, also slow to read, which is what step 2 removes.

To repeat it in WSL: `apt-get install mesa-va-drivers vainfo build-essential nasm pkg-config libva-dev ffmpeg`;
`modprobe vgem` (WSL has no `/dev/dri` without it; vgem gives `card0` and `renderD128`); `LIBVA_DRIVER_NAME=d3d12`
(`MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA` picks the RTX 4090); `vainfo --display drm --device /dev/dri/renderD128`
lists H.264 ConstrainedBaseline, Main and High VLD. Then publish the probe
(`dotnet publish src/Phantom.Relay.Tools.DecodeProbe -c Release -r linux-x64 --self-contained true`) and run
`Phantom.Relay.Tools.DecodeProbe sample.h264 --reference reference.yuv [--quiet]`. Samples:
`ffmpeg -f lavfi -i "testsrc2=size=2560x1440:rate=60,noise=alls=12:allf=t" -frames:v 120 -c:v libx264 -profile:v main -level 4.2 -bf 0 -g 120 -b:v 25M -maxrate 25M -bufsize 2M -bsf:v h264_metadata=aud=insert -f h264 sample.h264`,
reference `ffmpeg -i sample.h264 -vsync passthrough -f rawvideo -pix_fmt yuv420p reference.yuv` (without passthrough
FFmpeg doubles a frame and every picture after it differs).

### Not verified

- Anything on the Ally or SteamOS: radeonsi's VA driver, its decode and read-back times, that SteamOS's libva is found,
  Gaming Mode, and running inside Steam's runtime container (a non-Steam shortcut normally runs outside it).
- The console's real stream: only synthetic samples were decoded. The desktop head's whole stream on Linux (the window,
  pads and sound are the other lanes' work) did not run.
- The software fallback inside libavcodec (planar 4:2:0 copy): unit-tested as a copy, never reached live (every sample
  decoded on VA-API).
- `Flush` and a stream changing size mid-way.

### Cheaper read-back, before step 2

`av_hwframe_transfer_data` maps the surface and copies it into a frame of its own, then the decoder splits that copy.
Mapping it directly (`av_hwframe_map` with `AV_HWFRAME_MAP_READ`) and splitting straight from the mapping saves one
5.5 MB copy per 1440p picture. The read from GPU memory stays. It was not built: step 2 removes the read altogether.

## Bundling and the LGPL

`FfmpegLibraries.targets` adds `libavcodec.so.62` and `libavutil.so.60` from `artifacts/native/linux-x64/ffmpeg` as
`None` items (copied to the output and the publish folder) only when the RuntimeIdentifier is `linux-x64` and both
files exist. Without them the app still builds and the decoder says they are missing. The desktop and the probe import
it. A linux-x64 publish with the Windows script's settings (`PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`,
`EnableCompressionInSingleFile`, self-contained) carries them **inside** the single file. Checked with the probe: a
40.8 MB executable, nothing beside it, and on first start .NET unpacks both into `~/.net/<app>/<hash>/`, where the
decoder found them. So the linux-x64 publish (the publish lane) needs, before `dotnet publish -r linux-x64`:
`scripts/build/build-ffmpeg-linux.ps1` (once, or on a new FFmpeg pin). The publish script's scan for this machine's
paths looks at `Phantom.*` binaries only; the FFmpeg libraries were checked by hand (above). To ship them **beside** the
executable instead, which is the plainer LGPL story ("the user can replace the library"), set
`ExcludeFromSingleFile="true"` on the two items and ship the folder (the app, the two libraries, `COPYING.LGPLv2.1`).
The decoder loads them by soname through .NET's probing (the app's folder, then the system's), which was not tried
with the libraries beside a single-file app. That choice and the updater's handling of more than one file are the
owner's call.

What the LGPL asks of a binary release that uses FFmpeg's libraries dynamically, and the notice the public README
needs (proposed text):

> Nightcast for Linux uses FFmpeg's libavcodec and libavutil (https://ffmpeg.org), version 8.1.3, licensed under the
> GNU Lesser General Public License v2.1 or later, to decode video on the GPU through VA-API. They are an unmodified
> build of FFmpeg's own release source (https://ffmpeg.org/releases/ffmpeg-8.1.3.tar.xz), configured with the h264
> decoder, parser and VA-API hwaccel only, and are dynamically linked: Nightcast loads `libavcodec.so.62` and
> `libavutil.so.60` at run time, and any compatible build of FFmpeg 8 can replace them. The licence text is
> included with the release (COPYING.LGPLv2.1). FFmpeg is a trademark of Fabrice Bellard.

Also in the release: `COPYING.LGPLv2.1` (and FFmpeg's `LICENSE.md`), the exact configure line (`FFMPEG-BUILD.txt`), and
the source link or a copy of the tarball.

## Step 2 (design only): pictures left on the GPU, as DMA-BUFs into Dawn on Vulkan

The Windows path to follow: `MediaFoundationH264Decoder.TryTakeSurface` hands the decoded D3D11 surface on;
`SharedSurfaceRing` copies it on the GPU into a ring of NV12 textures shared by DXGI NT handles; `SharedPictureDecoder`
(an `IGpuPictureProducer`) publishes `GpuPictureRing(Generation, Slots: nint handles, Fence, w, h)` and per-picture slots
through `GpuPictureSlots`; the App's `GpuPicturePresenter` imports each slot once
(`IGraphicsDevice.ImportSharedTexture(nint)`), brackets each draw with `BeginSharedAccess(texture, fence, value)` /
`EndSharedAccess`, and draws with the engine's `Nv12PictureRenderer` (luma R8 plus chroma RG8 plane views). Engine
branch `relay/gpushare`: "Graphics: import textures another D3D device shares, by DXGI handle, on Dawn" and "Media: draw
NV12 pictures from a texture another device shares".

### Producer (Relay, Media.Linux)

1. A ring of K NV12 VA surfaces of the decoder's own (`vaCreateSurfaces`, `VA_RT_FORMAT_YUV420`, with the export usage
   hint), each exported **once** as DMA-BUFs: `vaExportSurfaceHandle(display, surface,
   VA_SURFACE_ATTRIB_MEM_TYPE_DRM_PRIME_2, VA_EXPORT_SURFACE_READ_ONLY | VA_EXPORT_SURFACE_SEPARATE_LAYERS, &desc)` gives
   per layer a DRM format (`DRM_FORMAT_R8` for luma, `DRM_FORMAT_GR88` for chroma), the object's fd, offset, pitch and
   modifier. Separate layers mean two single-plane imports, so no multi-planar format is needed on the Vulkan side.
2. Per picture, a GPU copy of the decoder's surface (`data[3]` of the VAAPI AVFrame is its `VASurfaceID`) into the
   next free ring slot through VA's video processing (`VAProfileNone`/`VAEntrypointVideoProc`, one
   `VAProcPipelineParameterBuffer`), mirroring `SharedSurfaceRing`. This keeps the decoder's pool and the renderer's
   imports apart: the decoder's own surfaces are recycled by libavcodec as it likes. (Exporting the pool's surfaces
   directly saves the copy but ties their lifetime to the renderer: holding AVFrames and raising
   `AVCodecContext.extra_hw_frames`, one more mirrored offset.)
3. Sync, first version: `vaSyncSurface` on the ring slot on the decode thread (a CPU wait of about the copy's GPU time),
   then publish with no fence, which the engine's access API already means as "the producer finished otherwise". Later:
   `DMA_BUF_IOCTL_EXPORT_SYNC_FILE` on the slot's buffer (Linux 6.0+) gives a sync file the renderer's queue waits on.
   Back from the renderer: its end-of-access sync file. The producer polls it before writing that slot again (a ring of
   at least three slots means it is nearly always signalled), or attaches it to the buffer with
   `DMA_BUF_IOCTL_IMPORT_SYNC_FILE` so VA's implicit sync waits on the GPU.
4. Same GPU: the producer opens the render node the renderer's Vulkan device is on (one GPU on the Ally; in general,
   from the device's DRM major and minor).
5. libva calls the decoder makes itself: `vaCreateSurfaces`, `vaDestroySurfaces`, `vaExportSurfaceHandle`,
   `vaCreateConfig`, `vaCreateContext`, `vaCreateBuffer`, `vaBeginPicture`, `vaRenderPicture`, `vaEndPicture`,
   `vaSyncSurface`, from `libva.so.2`. No FFmpeg build change (`--enable-libdrm` would only be needed for
   `av_hwframe_map` to `AV_PIX_FMT_DRM_PRIME`).

### Engine changes (an engine lane through the coordinator)

**Native, `phantom_gfx` on Dawn/Vulkan (Linux):**

- Ask for Dawn's features `SharedTextureMemoryDmaBuf` and `SharedFenceSyncFD` (and the Vulkan extensions they rest on:
  `VK_KHR_external_memory_fd`, `VK_EXT_external_memory_dma_buf`, `VK_EXT_image_drm_format_modifier`,
  `VK_KHR_external_semaphore_fd`, `VK_EXT_queue_family_foreign`), in the device request sokol_app makes (the same patch
  point as the D3D12 features). `DawnMultiPlanarFormats` is not needed with separate layers.
- A capability bit `PG_TEXTURE_SHARED_DMABUF = 0x8` beside `PG_TEXTURE_SHARED_HANDLES`, and the device's DRM node in
  `pg_capabilities` (from `VkPhysicalDeviceDrmPropertiesEXT`: `render_major`, `render_minor`) as the Linux counterpart of
  `adapter_luid`.
- An import for a DMA-BUF picture:
  ```c
  typedef struct pg_dmabuf_plane { int32_t fd; uint32_t offset; uint32_t stride; uint32_t drm_format; } pg_dmabuf_plane;
  typedef struct pg_dmabuf_picture {
      int32_t width, height;        // luma size; chroma is half each way (NV12)
      uint64_t modifier;            // DRM format modifier of the surface (DRM_FORMAT_MOD_LINEAR or the driver's tiling)
      pg_dmabuf_plane luma;         // DRM_FORMAT_R8
      pg_dmabuf_plane chroma;       // DRM_FORMAT_GR88
  } pg_dmabuf_picture;
  PG_API int32_t pg_import_dmabuf_picture(const pg_dmabuf_picture* picture, pg_shared_texture* out); // PG_SHARED_NV12
  ```
  Each plane becomes its own `wgpu::SharedTextureMemory` (`SharedTextureMemoryDmaBufDescriptor`: size, `drmFormat`,
  `drmModifier`, one `SharedTextureMemoryDmaBufPlane` {fd, offset, stride}), its texture R8Unorm or RG8Unorm, injected
  into sokol images as the D3D12 import does. The result is the same `pg_shared_texture` (`texture` luma, `chroma`), so
  everything above it is unchanged. Check in the vendored Dawn whether it duplicates the fd or takes it (the caller
  keeps its fds either way: dup before handing them on if Dawn takes them).
- Access on Vulkan needs layouts and sync files: `pg_begin_shared_access` chains
  `SharedTextureMemoryVkImageLayoutBeginState` (UNDEFINED to SHADER_READ_ONLY_OPTIMAL for a read-only import) and, when
  given, a `SharedFenceSyncFDDescriptor` fence; `pg_end_shared_access` reads `SharedTextureMemoryVkImageLayoutEndState`
  and hands back the end fence as a sync-file fd. New entry points rather than reusing the D3D ones, since a sync file
  is one signal, not a timeline value:
  `PG_API int32_t pg_begin_dmabuf_access(uint32_t texture, int32_t sync_fd /* -1: none */);`
  `PG_API int32_t pg_end_dmabuf_access(uint32_t texture, int32_t* sync_fd /* -1 when nothing to wait for */);`
  Both planes' memories begin and end together under the one texture id.

**C#, Phantom.Engine.Graphics:**

- `GpuTextureFeatures.SharedDmaBuf`, and `GpuCapabilities.DrmRenderNode` (major, minor; 0 where none).
- `readonly record struct DmaBufPlane(int Fd, uint Offset, uint Stride, uint DrmFormat)` and
  `readonly record struct DmaBufPicture(int Width, int Height, ulong Modifier, DmaBufPlane Luma, DmaBufPlane Chroma)`
  (value types: no allocation per import).
- On `IGraphicsDevice`, with refusing default bodies as for the D3D import:
  `GpuSharedTexture ImportDmaBufPicture(in DmaBufPicture picture)`,
  `void BeginDmaBufAccess(in GpuSharedTexture texture, int syncFd)` and `int EndDmaBufAccess(in GpuSharedTexture texture)`
  (the sync file to wait on, -1 for none). `Destroy(in GpuSharedTexture)` is reused.
- `SokolDevice` implements them over the new natives. The recording device records them, and the browser and Specter
  devices refuse them, as for the D3D import.
- `Nv12PictureRenderer` is unchanged: it draws a `GpuSharedTexture`'s two planes whoever imported them.

**Relay, once the engine has it:** `GpuPictureRing.Slots` holds DXGI handles (`nint`); the Linux ring needs a
`DmaBufPicture` per slot, so the ring and `GpuPicturePresenter` gain a platform-neutral slot description (a DXGI
handle or a `DmaBufPicture`) and the presenter picks the import by it. The fence becomes a sync file per picture on
Linux. A `SharedVaapiPictureDecoder` (Desktop/Pictures) plays `SharedPictureDecoder`'s part: reads back until the
window offers its device, then copies into the ring and publishes. Expected gain: the read-back (13.6 ms at 1440p in
WSL, unknown on the Ally) and the CPU upload of 5.5 MB a picture both go, which also saves the handheld's battery.
