# Nightcast

Play your own Xbox from a Windows PC or handheld (such as the MSI Claw) over your home network, with low latency,
any controller, and optional frame generation.

Nightcast is an **unofficial** remote-play client. It is not made, endorsed or supported by Microsoft or Xbox. It
uses your console's own remote-play feature; you need an Xbox Series X|S or Xbox One with
**Settings › Devices & connections › Remote features** turned on.

## Download

Get `Nightcast.exe` from the [latest release](../../releases/latest) and run it. It is a single file and needs no
installation. At every start it checks this page for a newer release and updates itself. Each download is checked
against the release's SHA-256 first. Start it with `--no-update` to skip that.

Windows 10 or 11, 64-bit. Windows may warn about an unknown publisher the first time.

## First start

1. Nightcast shows a short code. On any phone or computer, open **microsoft.com/link** and enter the code. Sign in
   with the Microsoft account that owns the console.
2. Nightcast finds your console and starts streaming, fullscreen. With one console it never asks which.

Your sign-in is kept on your own machine only, so later starts connect straight away. `--sign-out` forgets it.

## Controls

| On the pad | |
|---|---|
| View + Menu, short press | Nightcast's menu: stats, frame generation, upscaler, picture fit, rumble strength, fullscreen, recording, end |
| View + Menu, held 2 s | End the stream |

| On the keyboard | |
|---|---|
| F2 | Picture: 16:9 with bars, or stretched (for 16:10 screens) |
| F3 | Stats |
| F4 / F5 / F8 | Frame generation: off / LSFG / NVIDIA FRUC, its setting, x2 / x3 / x4 |
| F6 / F7 | Upscaler and its sharpness |
| F9 | Record a clip |
| F11 or Alt+Enter | Fullscreen or a window |
| Escape | End the stream (Shift+Escape while keyboard and mouse play) |

Xbox controllers, DualSense and DualShock 4 pads, and handheld built-in pads work, with rumble.

## Frame generation and upscaling (optional)

- **LSFG and LS1** use your own copy of [Lossless Scaling](https://store.steampowered.com/app/993090/Lossless_Scaling/)
  from Steam. Nightcast finds it through Steam and contains none of its files. Without it, these options are
  unavailable.
- **NVIDIA FRUC** needs an NVIDIA GPU and NVIDIA's Optical Flow SDK on your machine. Set `PHANTOM_NVOFFRUC_DIR` to
  its folder.
- FSR 1 and xBR upscaling are built in.

## Privacy

Nightcast talks only to Microsoft's sign-in and streaming services, your console, and this GitHub page for updates.
It sends nothing anywhere else and collects nothing.
