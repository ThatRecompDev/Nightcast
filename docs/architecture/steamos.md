# Nightcast on SteamOS

Nightcast runs natively on Linux as `Nightcast-linux-x64`: one self-contained file, built by
`scripts/publish/publish-desktop.ps1 -Runtime linux-x64` (a release carries it beside `Nightcast.exe`). The target is
SteamOS on a handheld (the owner's ROG Ally: AMD Z1 Extreme) in Steam's Gaming Mode, where gamescope shows the window
fullscreen through XWayland and Steam Input owns the controls.

**Status (2026-11-23): built and unit-tested on Windows only; nothing below has run on a Linux machine yet.** Since
engine 0.83.3-relay.2 the Linux file carries the engine's linux-x64 native libraries inside it (`libphantom_gfx.so` on
Dawn/Vulkan and X11, `libphantom_audio.so` on ALSA, `libphantom_media.so`; glibc 2.35 or later) beside FFmpeg's two
libraries. On Linux the engine compiles out its frame pacing, its shader cache and shared D3D textures, and its
XInput pads and GDI fonts are Windows-only, hence Nightcast's own Linux pads and fonts below.

## Running it in Gaming Mode

1. Copy `Nightcast-linux-x64` to the device (Desktop Mode), make it executable (`chmod +x`, or the file manager's
   properties), and add it to Steam: *Add a Non-Steam Game*, then pick the file.
2. In the shortcut's controller settings choose the **Gamepad** template (an Xbox 360 pad). Steam Input then hands
   Nightcast its virtual Xbox 360 pad, the Ally's or the Deck's own controls included; any pad Steam Input maps works
   the same way.
3. Start it from the library in Gaming Mode. The window opens fullscreen at once and shows the setup on screen, large
   and centred: the first time a sign-in code ("on any phone or computer, open microsoft.com/link and enter the
   code"), then the consoles to pick from (d-pad, A to choose, B to cancel; one console is taken without asking).
   Under gamescope the sign-in page is not opened on the device (a browser would cover the window): enter the code on
   a phone. A failure stays on screen until B. The account is kept, so later starts go straight to the stream.
4. In the stream, View and Menu together open Nightcast's menu; held for two seconds they end the stream.

The update check runs at start, before the window: a newer release's `Nightcast-linux-x64` replaces the file (made
executable again) and starts in its place. Whether Steam keeps tracking the game across that hand-over (the first
process ends, the new one goes on) is not checked yet.

## Pads (`Phantom.Relay.Pads.Linux`)

`EvdevPads` reads every pad the kernel reads as a gamepad from `/dev/input/event*` (a device with `BTN_SOUTH` and
`ABS_X`), each on a reader thread, into the engine's gamepad slots: buttons by place (xpad and Steam Input send the
button labelled X as `BTN_X`, Sony's and Nintendo's drivers send the top button as `BTN_NORTH`; the vendor decides),
sticks from -1 to 1 over each axis' own range with y down, triggers from `ABS_Z`/`ABS_RZ` (else `ABS_BRAKE`/`ABS_GAS`,
else their digital press), the d-pad from the hat, and rumble as one `FF_RUMBLE` effect (strong: the low-frequency
motor). The pads Steam lists in `SDL_GAMECONTROLLER_IGNORE_DEVICES` (the physical pads behind its virtual one) are
left alone, so a pad is not read twice. The seated user's access to the event devices comes from systemd's `uaccess`;
a device that cannot be opened is tried again every two seconds. Not checked on hardware: the ioctl numbers and
struct layouts are written from the kernel headers (and pinned by tests), the Steam virtual pad's ranges from xpad's.

## Fonts

The setup screen and the stats are drawn with a TrueType file found under fontconfig's folders (the user's own
first, then `/usr/local/share/fonts` and `/usr/share/fonts`): Consolas resolves to DejaVu Sans Mono, Noto Sans Mono or
Liberation Mono, whichever is installed; the setup's face (Segoe UI) to DejaVu Sans, Noto Sans or Liberation Sans.
Which of them SteamOS's base image ships is not checked; without any the window still runs, without text.

## Frame generation with lsfg-vk

Lossless Scaling's own frame generation (LSFG, LS1) and NVIDIA FRUC run on Windows' decoder only. On Linux Nightcast
runs lsfg-vk itself, inside its own process: no Decky, no plugin, no launch options. lsfg-vk (https://lsfg-vk.dev, by
PancakeTAS) is a Vulkan layer that runs Lossless Scaling's frame generation on the presents it sees, with the
player's own Lossless Scaling. The player needs Lossless Scaling from Steam on its **lsfg-vk** beta branch (Steam:
Lossless Scaling, Properties, Betas, `lsfg-vk`), which brings the `lsfg-vk.dll` lsfg-vk 2.0 reads; Nightcast finds
it in Steam's libraries (`LsfgVkLossless`) and says on the frame-generation row when it is missing or on the wrong
branch, and then downloads nothing.

The facts, checked 2026-11-23 against the official build and the 2.0.0 source (`Desktop/LsfgVk/LsfgVkRelease` pins
them in one place):

| What | Value |
|---|---|
| Version | 2.0.0 (its git tag; the Decky plugin decky-lsfg-vk 0.14.4 pins the same build) |
| Build | `https://builds.lsfg-vk.dev/lsfg-vk-2.0.0.tar.xz`, SHA-256 `08bdbdf373a111022df87dac7aa87e3b564bb841f961552e3ca85fea12b5aa74` |
| Licence | CC BY-NC-ND 4.0 since 2.0 (source `LICENSE.txt`; 1.x was MIT/GPLv3): no commercial use, no derivatives |
| Archive | `bin/lsfg-vk-cli`, `bin/lsfg-vk-ui`, `lib/liblsfg-vk-layer.so` (+ `.x86.so`), `share/vulkan/implicit_layer.d/VkLayer_LSFGVK_frame_generation.json` (+ `.x86.json`), the UI's desktop file and icon |
| Manifest | layer `VK_LAYER_LSFGVK_frame_generation`, `library_path` `../../../lib/liblsfg-vk-layer.so` (relative to the manifest), `disable_environment` `DISABLE_LSFGVK=1`; no enable variable |
| Configuration | TOML at `LSFGVK_CONFIG` (else `$XDG_CONFIG_HOME/lsfg-vk/conf.toml`): `version = 2`; `[global]` `dll`, `allow_fp16`, `log_level`, `log_file`; `[[profile]]` `name`, `active_in`, `pacing_mode` (`vsync` only), `multiplier` (1 = off), `flow_scale` (0.25 to 1), `performance_mode`, `override_present_mode`, `preserve_swapchain_image_count`; unknown keys are refused |
| Profile choice | `LSFGVK_PROFILE` (by name) first, then `active_in` against the executable's path suffix, a Wine `.exe`, the process name, then `SteamAppId`; no profile: the layer unloads |
| Live changes | the layer watches the file's folder (inotify, close-write or moved-to) and takes `multiplier`, `flow_scale` and `performance_mode` of its profile at the next present (it returns `VK_ERROR_OUT_OF_DATE_KHR` once so the swapchain is made again); the profile and everything else only at start |
| Load check | a `VkLSFGVersionInfo` (`sType` `0x4C534647`) chained to `vkCreateInstance` gets the layer's version string when it loaded and took a profile |

How Nightcast uses it (`Desktop/LsfgVk`):

1. **Nothing of lsfg-vk is in the release.** When the player turns frame generation on (the menu's *Frame
   generation* row or F4) Nightcast downloads the pinned build in the background, refuses it unless its SHA-256 is the
   pinned one, decompresses it with the system's `xz` and unpacks only the 64-bit layer and its manifest into
   `$XDG_DATA_HOME/Nightcast/lsfg-vk/2.0.0/` (else `~/.local/share/...`), keeping the archive's tree so the manifest's
   relative library path resolves. Nothing goes to `~/.local/lib` or a shared `implicit_layer.d`, so no other game
   loads it. The menu shows the notice: lsfg-vk 2.0.0 by PancakeTAS, lsfg-vk.dev, CC BY-NC-ND 4.0, downloaded from
   its official builds and run with the player's own Lossless Scaling.
2. **Nightcast's own configuration** (`Nightcast/lsfg-vk/conf.toml`, `LsfgVkConfig`): the `lsfg-vk.dll` found,
   half precision allowed, the layer's log beside it (`lsfg-vk.log`), and one profile named `Nightcast` with the
   player's multiplier (x2 to x4; off is 1), flow scale and performance mode from the existing rows (Frame generation,
   LSFG setting, Multiplier; F4, F5, F8). It is replaced by a rename, which the layer's watcher takes live.
3. **The layer is read when a Vulkan instance is made**, so it loads before the window opens: at start, when the
   player's kept choice (`lsfgvk.choice` in the user's store) is on, Nightcast sets in its own native environment (libc
   `setenv`; .NET's own setter does not reach `getenv`) what the Decky wrapper sets for a game:
   `VK_ADD_IMPLICIT_LAYER_PATH` (the manifest's folder), `LSFGVK_CONFIG`, `LSFGVK_PROFILE=Nightcast` (a non-Steam
   shortcut has no Steam app id of its own, so the profile is chosen by name), `ENABLE_GAMESCOPE_WSI=0` and
   `DXVK_HDR=0` (inert for Nightcast), and removes `DISABLE_LSFGVK`, `DISABLE_LSFG` and `LSFGVK_ENV`. Then it asks the
   loader (`VulkanLayerProbe`: `vkEnumerateInstanceLayerProperties`, then a bare instance with the version query). A
   loader that does not list the layer is older than `VK_ADD_IMPLICIT_LAYER_PATH` (Ubuntu 22.04's 1.3.204 ignores it):
   the version's `share` folder then goes first in `XDG_DATA_DIRS`, which every loader searches, and it asks again.
4. **Turned on while not loaded** (the first time, or after a start with it off), once the download is ready the
   screen says "Frame generation is ready: Nightcast restarts to load it" for three seconds, the window closes, the
   stream and the session end cleanly, and Nightcast `execv`s itself: the same file, arguments and process id (Steam
   keeps tracking it), the layer loaded this time. The kept account brings the stream back; with more than one
   console the player picks it again. **Loaded, every change is live**: off writes multiplier 1 (the layer passes the
   presents through), on or another multiplier or setting writes it, and the layer recreates its swapchain.
5. **While lsfg-vk generates, new pictures only is on by itself** (below), so it interpolates between the console's
   real pictures, never a repeat; the row shows "on (frame generation runs)". The menu's *Frame generation* row and
   the stats show the real state: `lsfg-vk: LSFG x2 → 60 fps (quality, flow 1.00)`, `off (lsfg-vk)`, downloading,
   ready to restart, or why not (Lossless Scaling not found or not on its branch, the download refused or failed, the
   layer not found or not loaded, with the path of its log).

Checked in WSL (Ubuntu 22.04, lavapipe): the real 2.0.0 layer, unpacked into this folder shape, is found through
`XDG_DATA_DIRS`, takes the `Nightcast` profile through `LSFGVK_PROFILE` ("identified via environment"), reports
`2.0.0` through the version query, and its log goes to `log_file`; `lsfg-vk-cli validate` accepts the configuration
Nightcast writes; the real archive (through `xz`) unpacks to exactly the two files. Not checked yet, on the Ally:
SteamOS's loader and `VK_ADD_IMPLICIT_LAYER_PATH`; the layer on Dawn's swapchain under gamescope through XWayland
(frames generated, pacing, HDR off); whether the engine recreates its swapchain after the layer's one
`VK_ERROR_OUT_OF_DATE_KHR` on a live change; the `execv` restart in Gaming Mode.

New pictures only can also be turned on by hand, for another external generator: the menu's *New pictures only* row,
F12, or `--external-frame-generation`. Then each frame waits for the game's next new picture (`NewPictureSignal`,
the decode thread's `RepeatClassifier` telling new pictures from the console's repeats; at most 100 ms) before it
draws, so the window presents at the game's own rate (30 a second for a 30 fps game) and the generator never
interpolates between two copies of one picture.

What this costs, and the engine change that would remove it: the engine presents every frame it runs
(`phantom_gfx`'s `pg_on_frame` calls `pg_wgpu_present` unconditionally), and its pacing call (`pg_set_paced_rate`,
`SokolWindowHost.Pace`) works on Win32 through Dawn only. So Nightcast cannot skip a present; it holds the frame
instead, and the pads are then read once a picture rather than once a refresh (up to one picture interval of extra
input delay, 33 ms at 30 fps). The exact fix is an engine lane: a call that ends the current frame **without
presenting** (for example `pg_skip_present()` / `ISokolWindow.SkipPresent()`: `pg_on_frame` then neither acquires
nor presents the surface texture), with the frame loop paced to the display's refresh by a timed wait when nothing is
presented (on Linux and Windows alike, since without a present FIFO no longer paces it). Nightcast would then run
every refresh, read the pads every refresh, and present only when `NewPictureSignal` says a new picture came.

## What is Windows-only

| Feature | On Linux |
|---|---|
| LSFG (Lossless Scaling's frame generation) | Nightcast's own D3D11 port is not run; lsfg-vk runs instead (above), from the same rows and keys |
| LS1 and SGSR (Lossless Scaling's upscalers) | chosen with F6, they fall back to FSR 1 (no decoder-side GPU path) |
| NVIDIA FRUC | not offered |
| Recording (F9: Media Foundation, NVENC, ffmpeg's comparison) | F9 records `source.mp4` alone: the console's H.264 and Opus sound as they came, by the pure C# MP4 writer, into `$XDG_VIDEOS_DIR` (else `~/Videos`) `/Nightcast/<stamp>/` ([recorder](recorder.md#on-linux-sourcemp4-with-sound)); no shown.mp4 and no comparison |
| Borderless fullscreen (Win32) | not needed: gamescope shows the window fullscreen |
| Media Foundation decoding | VA-API through FFmpeg ([linux-decode](linux-decode.md)), else openh264 in software |
| PlayStation pads over HID, XInput | evdev (above) |

## FFmpeg's LGPL notice (for the release README)

The Linux build bundles FFmpeg's libavcodec and libavutil for the VA-API decoder; a release carries
`COPYING.LGPLv2.1` and `FFMPEG-BUILD.txt` beside the Linux file. The notice for the public README
([linux-decode](linux-decode.md) has the details):

> Nightcast for Linux uses FFmpeg's libavcodec and libavutil (https://ffmpeg.org), version 8.1.3, licensed under the
> GNU Lesser General Public License v2.1 or later, to decode video on the GPU through VA-API. They are an unmodified
> build of FFmpeg's own release source (https://ffmpeg.org/releases/ffmpeg-8.1.3.tar.xz), configured with the h264
> decoder, parser and VA-API hwaccel only, and are dynamically linked: Nightcast loads `libavcodec.so.62` and
> `libavutil.so.60` at run time, and any compatible build of FFmpeg 8 can replace them. The licence text is
> included with the release (COPYING.LGPLv2.1). FFmpeg is a trademark of Fabrice Bellard.

## lsfg-vk's notice (for the release README)

Nightcast neither carries nor changes lsfg-vk; the notice for the public README:

> Frame generation on Linux uses lsfg-vk (https://lsfg-vk.dev) by PancakeTAS, version 2.0.0, licensed under Creative
> Commons BY-NC-ND 4.0. Nightcast does not include it: when you turn frame generation on, Nightcast downloads the
> unmodified official build (https://builds.lsfg-vk.dev/lsfg-vk-2.0.0.tar.xz) to your own machine, checks its SHA-256,
> and keeps it in Nightcast's own data folder, where only Nightcast loads it. lsfg-vk runs your own copy of Lossless
> Scaling (bought on Steam, beta branch "lsfg-vk"); neither is part of Nightcast, and neither project endorses it.
