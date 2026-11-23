# Nightcast on SteamOS

Nightcast runs natively on Linux as `Nightcast-linux-x64`: one self-contained file, built by
`scripts/publish/publish-desktop.ps1 -Runtime linux-x64` (a release carries it beside `Nightcast.exe`). The target is
SteamOS on a handheld (the owner's ROG Ally: AMD Z1 Extreme) in Steam's Gaming Mode, where gamescope shows the window
fullscreen through XWayland and Steam Input owns the controls.

**Status (2026-11-23): built and unit-tested on Windows only; nothing below has run on a Linux machine yet.** The
window, the sound and openh264 need the engine's linux-x64 native libraries (`phantom_gfx` on Dawn/Vulkan, ALSA sound,
the media libraries), which arrive with engine version 0.83.3-relay.2; until Relay pins that version the Linux file
builds but cannot open its window.

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

Lossless Scaling's own frame generation (LSFG, LS1) and NVIDIA FRUC run on Windows' decoder only. On SteamOS the
community's lsfg-vk (a Vulkan layer, installed through Decky) generates frames between the presents it sees. For that,
turn on **new pictures only**: the menu's *New pictures only* row, F12, or start Nightcast with
`--external-frame-generation` (in the shortcut's launch options). Then each frame waits for the game's next new
picture (`NewPictureSignal`, the decode thread's `RepeatClassifier` telling new pictures from the console's repeats;
at most 100 ms) before it draws, so the window presents at the game's own rate (30 a second for a 30 fps game) and
lsfg-vk never interpolates between two copies of one picture.

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
| LSFG (Lossless Scaling's frame generation) | not run: the menu's frame generation row says so; use lsfg-vk |
| LS1 and SGSR (Lossless Scaling's upscalers) | chosen with F6, they fall back to FSR 1 (no decoder-side GPU path) |
| NVIDIA FRUC | not offered |
| Recording (F9: Media Foundation, NVENC) | refused, with the reason in the title |
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
