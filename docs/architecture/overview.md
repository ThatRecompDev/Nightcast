# Architecture overview

Phantom.Relay streams the player's own Xbox console (the service calls it xHome) to any engine target: the desktop
(Windows, Linux and the Steam Deck, macOS), the browser (and through it phones and tablets) and the PS5 homebrew
console. It plays with any pad the engine reads, keeps a profile per game, and can generate frames between the
stream's own (a 30 fps game shown at 60).

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
| Adapters | `Phantom.Relay.Http.Bcl`, `Phantom.Relay.Pads.Windows` | Xbox, engine Core, Hosting | started: the desktop's `IHttpExchange` over the BCL HTTP client, until the engine's HTTPS port lands; DualSense, DualSense Edge and DualShock 4 on Windows over USB and Bluetooth through Win32 HID (`PlayStationPads`: the engine's gamepad source, feedback and families, plus a motion side channel; [pads](pads.md)), read live from a DualSense over USB; a candidate for the engine's Platform.Desktop |
| Stream | `Phantom.Relay.Stream` | Xbox, Profiles, engine Net.WebRtc, Media, Audio, Hosting | planned: one session from sign-in to pictures, sound and pads |
| App | `Phantom.Relay.App` | Stream, Profiles, Xbox, engine Ui, Hosting, Graphics | planned: the screens (sign-in, consoles, profiles, the stream and its stats) |
| Heads | `Phantom.Relay.Desktop`, `.Browser`, `.Specter` | App and every layer above, engine Platform | planned: the composition roots |
| Tools | `Phantom.Relay.Tools.Probe` | Adapters, Profiles, Xbox, engine Platform.Desktop | started: the live probe: sign in, list the consoles, start a session, see whether the console answers an offer, stop ([probe](probe.md)) |
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

## The browser

The service's sign-in and REST endpoints refuse a page's requests from another origin (no CORS headers). The browser
head therefore needs a small same-origin relay for the REST calls only; pictures and sound go peer to peer. The
relay's design waits for the engine's HTTPS port.

## Frame generation

The console streams 60 frames a second; a 30 fps game repeats each picture. The planned pass finds the new pictures,
estimates optical flow between them at half or quarter resolution, checks it both ways for occlusion, and warps and
blends the pictures in between onto the display's clock. It costs about one source frame of latency. It belongs in
the engine (any video could use it) and lands after WebRTC and the pads.
