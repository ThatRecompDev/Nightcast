# Architecture overview

Phantom.Relay streams the player's own Xbox console (the service calls it xHome) to any engine target: the desktop
(Windows, Linux and the Steam Deck, macOS), the browser (and through it phones and tablets) and the PS5 homebrew
console. It plays with any pad the engine reads, keeps a profile per game, and can generate frames between the
stream's own (a 30 fps game shown at 60). How it runs on SteamOS (Gaming Mode, Steam Input, lsfg-vk) and what is
Windows-only: [steamos](steamos.md).

## Latency and quality come first

The owner's standing requirement: the lowest latency and the highest picture quality the console gives. Every design
choice is weighed against both, and a choice that costs either says so in its guide with its measured cost.

- **Quality**: the highest tier the service grants: 1440p by default (remote play's top tier since 2026; the console
  must output 1440p or 4K), falling back to 1080p on the high bitrate ladder. No extra scaling step when the display
  matches the stream, the engine's sharp upscaler only when it does not. The client reports a 1440p display so its
  description never caps the tier.
- **Latency budget**: every stage is measured each frame and shown in the stats overlay: network (RTP arrival),
  frame assembly, decode, upload, present. A picture is shown the moment it is decoded, on the next display refresh;
  nothing waits for a fixed playout delay beyond what the jitter of the link needs.
- **No coarse sleeps on the network path**: a timed sleep on Windows lasts a whole ~15.6 ms timer tick. With one
  between steps, the first live ICE run took 39 ms; without it, it took 11 ms, and the measured round trip to the
  console on the LAN is 0.4 ms (2026-11-19). The stream's network loop wakes on a datagram's arrival, never on a
  timer. That needs a wait-for-datagram call on the engine's transport port (an engine lane); until then the probe
  yields the thread instead.
- **Jitter buffer**: as small as the link allows, adapting down as soon as the link is steady; a lost packet is
  asked again (NACK) only while the answer can still arrive in time, otherwise a key picture is requested.
- **Decode**: on a thread of its own where the platform has threads; hardware decoding where a platform offers it
  (openh264 in software is the baseline everywhere). The decoder port must not add a frame of delay.
- **Input**: pad reports go out the moment the pad changes, never batched to the frame; the poll rate is the pad's
  own.
- **Frame generation is opt-in per profile**: it costs about one source frame (33 ms at 30 fps) by its nature, since
  it needs the next picture before it can show the one in between. The default profile leaves it off; the stats
  overlay shows its cost when on.

## Layers

The layer table below is the one `Phantom.Relay.Architecture.Tests` enforces. A layer gains its row before its first
project lands.

| Layer | Projects | May reference | Status |
|---|---|---|---|
| WebRtc | `Phantom.Relay.WebRtc` | engine Net, Core; BouncyCastle | started. Game-agnostic: it moves into the engine as `Net.WebRtc` once it carries a stream (owner's call, 2026-11-19). STUN (RFC 8489) with integrity and fingerprint, checked on RFC 5769's vectors; a controlling ICE agent (RFC 8445) measuring the path's round trip; a DTLS-SRTP client (RFC 5764, BouncyCastle) authenticated by fingerprint, exporting the SRTP keys. SCTP over DTLS (RFC 9260/8261): one association as a pure state machine over `ISctpOutput`, with CRC-32C, the four-way handshake, ordered and unordered DATA with fragmentation, SACK gap reports, RTO and congestion control, SHUTDOWN/ABORT and FORWARD TSN partial reliability; a 1.5 MB receive window; nothing allocated after construction. Data channels (RFC 8831/8832): `DataChannelHub` opens channels with DCEP on even streams as the DTLS client and carries string and binary messages. Not yet: stream reset to close channels (RFC 6525). SRTP/SRTCP (RFC 3711, 7714: AES-CM-HMAC-SHA1-80 and AES-128-GCM with our own GHASH, since BouncyCastle's is obsolete-marked and allocates; replay window, rollover counter), checked on the RFCs' vectors; RTP parsing with RFC 8285 extensions; a latency-first reorder stage (in-order packets never wait, holes give up after a few ms and are listed for NACK); NACK, PLI and receiver-report writers; H.264 (RFC 6184 mode 1) depacketized to Annex-B, dropping broken units until the next IDR. Every per-packet path is allocation-free (measured) |
| Xbox | `Phantom.Relay.Xbox` | engine Core | started: the sign-in chain, the service's REST calls, the console's channel messages and input reports ([xhome](xhome.md)) |
| Audio | `Phantom.Relay.Audio` | engine Audio | started: the console's game sound. `OpusStreamPlayer` takes the Opus RTP payloads (PT 111) from the stream and plays them through the engine's `OpusDecoder` and pull device (`IAudioDevice`; Sokol/WASAPI in the head) behind an adaptive, latency-first jitter buffer: 20 ms to start, 10 to 60 ms from measured jitter, reordering, concealment of losses (FEC is built and tested but waits on the engine's decoder, which skips in-band FEC: an engine lane), late drops, and clock drift held by resampling within ±0.5 %; lock-free and allocation-free between the network and audio threads (measured) |
| Profiles | `Phantom.Relay.Profiles` | Xbox, engine Content, Hosting, Core | started: per-game profiles and the stick and trigger shaping they set, kept in the engine's user store; keyboard and mouse played as the pad (`KeyboardMouse/`): `KeyboardMouseMapper` turns each engine `InputFrame` into an `XboxPadState` (bound keys, mouse buttons and wheel notches to buttons, triggers and stick directions, WASD to the left stick); `MouseStick` turns mouse speed into right-stick tilt through sensitivity, an exponent curve, an anti-dead-zone, short smoothing and a fast decay, allocation-free (measured); `GtaKeyboardMousePreset` ships a GTA-style profile. Input (`Input/`): `PadComposer` turns each `InputFrame` into the console's `XboxPadState` under the active profile (the chosen pad by place with dead zones through the engine's `GamepadStick.Shape`, curve and remaps, merged with keyboard and mouse); `PadReportPolicy` sends a change the moment it is composed, repeats an unchanged pad every 100 ms, ignores moves below one axis step and sends the pad at rest when focus is lost; both allocate nothing per frame (measured) |
| Adapters | `Phantom.Relay.Http.Bcl`, `Phantom.Relay.Pads.Windows` | Xbox, engine Core, Hosting | started: the desktop's `IHttpExchange` over the BCL HTTP client, until the engine's HTTPS port lands; DualSense, DualSense Edge and DualShock 4 on Windows over USB and Bluetooth through Win32 HID (`PlayStationPads`: the engine's gamepad source, feedback and families, plus a motion side channel; [pads](pads.md)), read live from a DualSense over USB; a candidate for the engine's Platform.Desktop. `Phantom.Relay.Pads.Linux`: every pad the Linux kernel reads as a gamepad, through evdev (`EvdevPads`: on SteamOS Steam Input's virtual Xbox 360 pad; buttons by place, sticks and triggers over each axis' own range, the hat as the d-pad, rumble as an `FF_RUMBLE` effect; the pads Steam lists in `SDL_GAMECONTROLLER_IGNORE_DEVICES` left to Steam; [steamos](steamos.md)), tested on fakes only, not yet on a Linux machine; also an engine candidate. `Phantom.Relay.Media.Windows/Recording`: an H.264 stream remuxed into MP4 as it is (`Mp4H264Remuxer`), NV12 textures of a D3D11 device encoded into MP4 on the GPU (`Mp4VideoEncoder`: Media Foundation's sink writer with hardware transforms, NVENC on the owner's GPU) and an MP4 read back (`Mp4Reader`), game-agnostic and an engine candidate (the engine has an MP4 demuxer only). `Phantom.Relay.Media.Linux`: H.264 on the GPU through VA-API on Linux (SteamOS), FFmpeg's libavcodec with its VA-API hwaccel from an LGPL build of FFmpeg's own loaded at run time (`VaapiH264Decoder`, pictures read back to NV12), an engine candidate ([linux-decode](linux-decode.md)) |
| Lsfg | `Phantom.Relay.Lsfg` | Adapters (Media.Windows' shared D3D11 interop), engine Core | started, optional, Windows-only, off by default, turned on per profile: Lossless Scaling's frame generation from the user's own install, reflected at run time and run on the decoder's D3D11 device on its GPU picture path; NV12 to RGBA and back on the video processor (`LsfgFrameStage`); a frame pacer ([lsfg](lsfg.md)) |
| Nvidia | `Phantom.Relay.Nvidia` | Lsfg (its frame-generation seam, colour conversions and timer), Adapters, engine Core | started, optional, Windows-only, off by default: NVIDIA's FRUC on the optical flow accelerator, loaded at run time from the owner's own Optical Flow SDK, as a second frame generator behind LSFG's seam (`FrucFrameStage`), switched live with F4 ([nvidia-fruc](nvidia-fruc.md)) |
| Stream | `Phantom.Relay.Stream` | Xbox, Profiles, WebRtc, Audio, engine Net, Media, Core | started: `StreamConnection`, one session from start to stop on one network thread of its own that yields between turns and never sleeps on a timer (until the engine's wait-for-datagram call): the session through `IXHomeSessionService`, the offer (`StreamOffer`), ICE, DTLS, SCTP and the channels (`ConsoleLink`, `ConsoleChannels`), the media (`MediaReceiver`: SRTP, reorder, H.264 depacketizing, PLI on loss), a keep-alive every 30 s, and the session stopped at the end; errors end it as `Failed` with a reason. Video is decoded by the head's `IVideoDecoder` on a `DecodeWorker` thread into the `PictureMailbox` (read back to the CPU), or, for a decoder that is also an `IGpuPictureProducer`, handed to the renderer on the GPU (`Media/Gpu`: the renderer's `IGpuPictureSource`, a `GpuPictureRing` of shared handles opened once, each picture a small `GpuPicture` value through `GpuPictureSlots`, a lock-guarded slot state machine paced by an `IPicturePacer`; see "The GPU picture path" below); the worker times the hand-off and classes each unit as a new picture or a repeat (`RepeatClassifier`, see "Frame generation" below), and offers each unit, before it is decoded, to the connection's `AccessUnitTap` (a listener that must only queue it: a recorder's), through which any thread can also ask the console for a key frame (sent as a PLI on the network thread's next turn, at most as often as a loss's); each Opus payload goes to the head's `IStreamAudioSink` from the network thread; `SendPad` keeps the newest state per pad (`PadMailbox`) and the network thread writes it onto the input channel at once. The console's title message picks the profile. `StreamStats` is an immutable snapshot (state, ICE round trip, DTLS time, frames received and decoded per second, decode average and worst, losses, keyframe requests, input reports) published on each change and once a second. Also the text sign-in and console choice the probe and the desktop share |
| App | `Phantom.Relay.App` | Stream, Profiles, Xbox, engine Ui, Hosting, Graphics, Media, Text | started: `StreamApplication`, the stream on screen as an engine `IApplication`: the newest picture letterboxed, straight from the decoder's GPU where the device shares textures (`GpuPicturePresenter`, the engine's `Nv12PictureRenderer`), else read back and drawn through the engine's `MovieRenderer`; each picture's decode-to-present time and each frame's CPU time for its picture measured (`PresentMeter`); each frame's input gathered into pad 0 by an `IPadComposer` (today `DirectPadComposer`, the engine's first pad slot as it is) and sent the moment it changes; F3 shows the stats as a small block in the window's top-left corner (`StatsOverlay`: the engine's canvas and text, a system font the head gives), short in the window's title and in full in the log; F4 cycles frame generation through off, LSFG and NVIDIA FRUC, F5 steps through LSFG's settings and F8 the multiplier (x2, x3, x4: up to 120 fps from a 30 fps game, capped to what the display shows), live (`FrameGenerationSwitch`); a window larger than the picture shows it upscaled (see "Upscaling" below), F6 steps through FSR 1, off and xBR; F9 starts or stops a recording through the head's `IStreamRecorder`, its status (`● REC 00:42`, where it was saved, the comparison's progress) in the title and over the picture ([recorder](recorder.md)); F1 or the menu saves the console's picture as a PNG through the head's `IScreenshots`, its status shown the same way ([screenshots](screenshots.md)); F12 (the menu, `--external-frame-generation`) presents only the game's new pictures, for an external frame generator such as lsfg-vk: each frame waits for the decode thread's `NewPictureSignal` before it draws, so the window presents at the game's own rate and never a repeat (the engine presents every frame it runs, so the frames are paced rather than presents skipped; [steamos](steamos.md) says what engine call would make it exact); where the decoder has no GPU path (Linux) the head's `IExternalFrameGeneration` (lsfg-vk, run inside Nightcast: [steamos](steamos.md)) takes F4 (off and on), F5 and F8 and the menu's rows, and turns new pictures only on by itself while it generates; Escape or closing the window stops the stream and the session. The setup on screen (`Setup/`): `SetupApplication` opens the window with the sign-in code, the consoles to pick from (the pad or the keys) and what went wrong, large and centred (`SetupText`), its lines also on the text console (`ScreenWriter`), then hands the same window to `StreamApplication` once a console is chosen; a failure stays until B or Escape (`SetupSession`, tested on the probe's fake account and service). Not yet: the profile screens |
| Heads | `Phantom.Relay.Desktop`, `.Browser`, `.Specter` | App and every layer above (Lsfg and Nvidia too: the desktop runs the frame generation on its decoder), engine Platform | started: `Phantom.Relay.Desktop`, the desktop's composition root: the updater before the window, then the window with the setup on screen (the kept account or a sign-in by code, the console choice), the default profile's tier, then `StreamConnection` with Media Foundation's hardware decoder (`Pictures/SharedPictureDecoder`: moved to the window device's adapter and its pictures shared on the GPU; openh264 where there is none), drawn by `StreamApplication` in a resizable 1280x720 Sokol window, the pads read through the engine's `XInputGamepads`; F9 records the stream into the user's Videos folder (`Recording/StreamRecorder`: the console's H.264 as it came, every picture shown encoded on NVENC, and ffmpeg's side-by-side comparison where the user has ffmpeg; [recorder](recorder.md)); F1 saves a screenshot (`Stills/ScreenshotTaker`); a finished recording is marked by a `done` file and a screenshot appears only whole ([screenshots](screenshots.md)). The browser and PS5 heads are planned |
| Tools | `Phantom.Relay.Tools.Probe` | Adapters, Profiles, Xbox, engine Platform.Desktop | started: the live probe: sign in, list the consoles, start a session, see whether the console answers an offer, stop ([probe](probe.md)); `Phantom.Relay.Tools.DecodeProbe`: an H.264 sample decoded on VA-API with each picture's timing and its difference from a reference decode ([linux-decode](linux-decode.md)) |
| Tests | `*.Tests` | the project under test, engine Testing, Architecture | landed |

## Where each piece comes from

| Piece | From |
|---|---|
| Sign-in, consoles, sessions, signaling over REST | Relay (Xbox) |
| HTTPS requests | Relay's `IHttpExchange` port today; the engine's HTTPS client port when its lane lands (desktop and browser use the BCL client meanwhile; the PS5 waits for the engine port) |
| WebRTC: ICE, DTLS-SRTP, SCTP data channels, RTP | engine lane `Net.WebRtc` (to be opened through the coordinator); the browser's own RTCPeerConnection behind the same port |
| H.264 pictures | engine `Media.Vpx` (openh264), drawn by `Media`'s YCbCr shader |
| Opus sound | engine `Audio` (our own decoder) and its output port |
| Sound to the controller's headphone jack | engine lane (planned): choosing the audio output device. The engine's sokol_audio output plays to the system default only. A profile then sends sound to the default, the pad in use or a named device. The jack is a device where the system exposes it: an Xbox pad over USB or the Xbox Wireless Adapter, a DualSense over USB, the Steam Deck. The headset's microphone can later carry party chat (the `chat` channel) |
| DualSense jack and speaker **over Bluetooth** | engine lane (planned, research done 2026-11-19): public descriptions show it works. Opus frames go inside the pad's Bluetooth HID output reports, beside its haptics. One report about every 10.7 ms. Routing to the jack or the speaker is set by the control report. Every report carries a CRC-32 seeded with one extra byte. It needs an Opus *encoder* (the engine decodes only) and evenly paced HID writes; it bypasses the system mixer, so it adds no virtual-cable delay. The exact report sizes, sub-packet ids and haptics rate differ between sources and are settled on a real pad. An Xbox pad cannot do this: its firmware carries no headset audio over Bluetooth |
| Pads, rumble, light | engine `Hosting` gamepads; DualSense/DS4 on the desktop and the browser's Gamepad API: engine lane (on top of `xinput-rumble`) |
| Frame generation and upscaling | engine lane (later): optical-flow interpolation as a compute pass; the picture upscaler exists |
| UI, text, storage | engine `Ui`, `Text`, `Content.Storage` |

## The GPU picture path

A decoded picture never touches the CPU where the platform lets two devices share a texture (Windows: the engine's
WebGPU device on D3D12 and the decoder's D3D11 device on one adapter). The seam is game- and platform-agnostic, in
Stream: the decode thread's `IGpuPictureProducer` and the render thread's `IGpuPictureSource`.

1. The window's device exists after the decoder: at its `Initialize` the app offers the device's adapter
   (`GpuCapabilities.AdapterLuid`). Before its next access unit the decode thread moves the decoder onto that adapter
   (a PC with an integrated GPU has two), only before the first picture since a new decoder needs a key frame.
2. A decoder's own surfaces cannot be shared, so each picture is copied on the GPU into a slot of a ring of shared
   NV12 textures (`SharedSurfaceRing`, `D3D11_RESOURCE_MISC_SHARED | SHARED_NTHANDLE`), and a shared D3D11 fence is
   set once the copy is done. Before a slot is written again the decoder's queue waits, on the GPU, for the value the
   renderer's last read of it ends at (`EndSharedAccess`), so neither thread ever blocks on the other.
3. A picture travels as a `GpuPicture` value (ring generation, slot, fence value, size, colour, decode time); nothing
   is allocated per picture. The renderer opens the ring's handles once (again when the stream's size makes a new ring),
   and each frame: `BeginSharedAccess`, draw letterboxed with the padded slot cropped, `Commit`, `EndSharedAccess`, the
   read's end handed back.
4. Anything missing (software decoding, no shared textures, an adapter that will not move, a ring the device refuses)
   leaves the pictures on the read-back path, and the F3 stats say why.

Measured on the owner's RTX 4090 and 240 Hz display (2026-11-19, a synthetic 1440p stream at 60 fps decoded on DXVA
and drawn by the engine's own device in a window, 600 frames per path):

| | Read back (before) | On the GPU (after) |
|---|---|---|
| Decode to present, average | 10.8 ms | 6.3 ms |
| Decode to present, worst | 15.0 ms | 7.0 ms |
| Decode thread CPU per picture (decode and hand-off) | 4.8 ms | 0.9 ms |
| of it the hand-off (read-back, or the copy's queueing) | 4.2 ms | 0.1 ms |
| Render thread CPU per frame for the picture | 0.27 ms (upload) | 0.01 ms (access, draw) |

The decode-to-present time includes the wait for the display's next refresh, as a real frame's does.

## Upscaling

The console streams 1440p at most: reporting a 4K display in `x-ms-device-info` and in the control channel's
`ClientDescription` still gets `imageattr x=[0:2560],y=[0:1440]` (checked live, 2026-11-19), so the request side stays
as it is and the picture is scaled up on this side. Where the window shows it larger than it came (the owner's 3840 x
2160 display), `StreamPicture` draws it (decoded or generated, NV12, from either path) at its own size into a target,
and `StreamUpscaler` scales it with the engine's upscale shader (`BuiltInShader.Upscale`, the one the engine's
`PictureUpscaler` uses for UI pictures): FSR 1's EASU into a target of the letterboxed rectangle's size, then RCAS
(sharpness 0.2 stops, `PictureUpscaler.FsrSharpness`) straight into that rectangle of the window's pass; xBR is one
pass. The targets are kept until a size changes, and the first two passes run only for a new picture, so a refresh
that repeats the picture costs RCAS alone. Frame generation stays at the stream's size (cheaper). A window no larger
than the picture is drawn bilinearly as before.

The profile's `Upscaling.Method` chooses it (`Fsr`, the default; `Bilinear` is off; `Xbr`; the older name `Sharp`
reads as `Fsr`; `Ls1`, `Ls1Performance` and `Sgsr` are Lossless Scaling's own, run where the pictures are decoded,
below); F6 steps through FSR 1, LS1, LS1 performance, SGSR, xBR and off live, F7 through RCAS's sharpness (0.2, 0.1,
0, 0.5 stops) or, with LS1, its network. The F3 stats show `FSR 1 → 3840x2160 0.16 ms` (the GPU
time of the passes before the window's: the picture's and EASU's, timed by the device while the stats show) or
`upscale off`, the title the filter's name.

Measured on the owner's RTX 4090 (2026-11-19, `StreamUpscalerLiveTests`, the engine's device, 2560 x 1440 to 3840 x
2160, each pass timed by the device): the picture's pass 0.01 ms, EASU 0.15 ms, RCAS 0.04 ms, against 0.02 to 0.03 ms
for the bilinear draw before; nothing allocated on the managed heap per frame. `PictureUpscaler` itself makes new
targets on every call (it upscales a UI picture once), so a per-frame mode with kept targets is an engine candidate.

**Lossless Scaling's upscalers** ([lossless-upscalers](lossless-upscalers.md)) run on the decoder's side of the GPU
picture path: the renderer asks the decoder for its pictures at the letterbox's size (`IGpuPictureSource.Scale`), the
decoder makes its shared ring at that size and upscales each picture it publishes (after LSFG, which stays at 1440p),
and the renderer draws it one to one. Without the GPU path or Lossless.dll, FSR 1 upscales here instead.

**Fullscreen** (F11, Alt+Enter) is not there: the engine's `IWindow` has no fullscreen call and phantom_gfx exposes
no `sapp_toggle_fullscreen`. It is an engine lane (a window call any app could use), not Relay's.

## The browser

The service's sign-in and REST endpoints refuse a page's requests from another origin (no CORS headers). The browser
head therefore needs a small same-origin relay for the REST calls only; pictures and sound go peer to peer. The
relay's design waits for the engine's HTTPS port.

## Frame generation

The console streams 60 frames a second; a 30 fps game repeats each picture. `RepeatClassifier` (Stream) tells the
repeats from the new pictures by each access unit's encoded size: a repeat is a P-frame with every macroblock skipped,
a few hundred bytes at 1440p, a new picture of a game in motion tens of kilobytes (`RepeatSettings.RepeatBytes`, 2 KB).
Size alone would take a quiet scene's small new pictures for repeats, so it follows the cadence: every unit is new
until eight units in a row alternate small and large; then units alternate between a new phase (new whatever their
size) and a repeat phase (a repeat when small); one large unit in the repeat phase is a slip that moves the phase on,
three in a row drop back to every unit new. When the game falls below 30 fps the console sends a picture for three
units (25 fps: N r N r r ...); a unit in the new phase clearly smaller than the one before (`HoldPercent`, after a unit
above `RepeatBytes`) is that held picture, a repeat, and the phase moves on by one, so frame generation is never fed
one picture twice. A second refinement nearly as large as the first is missed and caught one unit later as a slip
(one doubled picture). Key frames are always new. The counts a second and the cadence show in the
F3 stats and in a debug log line once a second. The threshold is the owner's observation, not yet measured on a long
capture: a live GTA run must confirm it.

The planned engine pass finds the new pictures,
estimates optical flow between them at half or quarter resolution, checks it both ways for occlusion, and warps and
blends the pictures in between onto the display's clock. It costs about one source frame of latency. It belongs in
the engine (any video could use it) and lands after WebRTC and the pads. Until then, the optional `Phantom.Relay.Lsfg`
runs Lossless Scaling's frame generation from the owner's own install ([lsfg](lsfg.md)), and the optional
`Phantom.Relay.Nvidia` NVIDIA's FRUC from the owner's own Optical Flow SDK ([nvidia-fruc](nvidia-fruc.md)), behind one seam
(`IFrameGenerationStage`); F4 switches between them live.
