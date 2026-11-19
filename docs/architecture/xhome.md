# xHome: the console streaming protocol

What `Phantom.Relay.Xbox` speaks. Microsoft documents none of it; it is what the public web player (xbox.com/play)
and the Xbox app send, written down here in our own words. Every constant lives once in `XboxEndpoints`,
`XHomeProtocol` or `XHomeChannels`.

## Sign-in

| Step | Call | Gives |
|---|---|---|
| 1. Microsoft account | the player signs in at `login.live.com/oauth20_authorize.srf` (the Xbox app's public client id, scope `service::user.auth.xboxlive.com::MBI_SSL`, response type `code`); the page ends on `oauth20_desktop.srf?code=...`; `POST oauth20_token.srf` trades the code (later the refresh token) | access and refresh tokens |
| 2. Xbox Live user | `POST user.auth.xboxlive.com/user/authenticate`, `RpsTicket: "t=" + access token`, contract version 1 | user token, user hash |
| 3. XSTS | `POST xsts.auth.xboxlive.com/xsts/authorize`, relying party `http://gssv.xboxlive.com/`, sandbox `RETAIL` | XSTS token; a 401 carries `XErr` (no profile, country, child account) |
| 4. Streaming | `POST xhome.gssv-play-prod.xboxlive.com/v2/login/user`, offering `xhome`, header `x-gssv-client: XboxComBrowser` | `gsToken`, its lifetime, the regions and their base addresses |

Only the Microsoft account tokens are kept (the user's store, key `account.credentials`). A refresh keeps the XSTS
and streaming tokens; a new sign-in drops them. Each token is renewed two minutes before it expires.

## Sessions

Every call goes to the default region's base address with `Authorization: Bearer <gsToken>`, `x-gssv-client` and
`x-ms-device-info` (a desktop browser's description; with the play request's OS name it decides the tier).

| Call | Purpose |
|---|---|
| `GET v6/servers/home` | the consoles: server id, name, type, power state |
| `POST v5/sessions/home/play` | start: server id, settings (`nanoVersion: "V3;WebrtcTransport.dll"`, `osName` android = 720p, windows = 1080p, tizen = 1080p high or 1440p, see below) |
| `GET {session}/state` | `WaitingForResources`, `Provisioning`, `ReadyToConnect`, `Provisioned`, `Failed` (with `errorDetails`) |
| `POST {session}/sdp`, then `GET` until answered | the offer with the channel versions (chat 1, control 1-3, input 1-8, message 1) and the chat format (Opus in WebM, 24 kHz mono); the answer is JSON inside `exchangeResponse` |
| `POST {session}/ice`, then `GET` until answered | the client's candidates plus `a=end-of-candidates`; the console's come back as `a=candidate:...` |
| `POST {session}/keepalive` | every 30 seconds |
| `DELETE {session}` | stop |

## Data channels

The offer carries audio (Opus, sendrecv: game sound and chat), video (H.264, recvonly) and four data channels:

| Label | Subprotocol | Use |
|---|---|---|
| `input` | `1.0` | binary reports (below), ordered |
| `control` | `controlV1` | JSON: on open the authorization request (a fixed access key, part of the protocol) and pad 0 added; key-picture requests |
| `message` | `messageV1` | JSON: the handshake; after its acknowledgement the client's description (system UI, install id, orientation, touch, capabilities, size); every `TransactionStart` is answered with `TransactionComplete` |
| `chat` | `chatV1` | party chat, not used yet |

## Input reports

Little-endian. Header: `u16 kind`, `u32 sequence`, `f64 milliseconds` (14 bytes). Client metadata adds `u8` most
touch points. A gamepad report adds `u8 count` and per pad `u8 index`, `u16 buttons`, `i16` LX LY RX RY (+Y up),
`u16` LT RT, `u32` physical and virtual physicality (23 bytes). The console's vibration report: `u8 kind (128)`,
`u8 type`, `u8 pad`, four `u8` motor percentages, `u16 duration`, `u16 delay`, `u8 repeat`.

## Confirmed live

- The plain account chain (Microsoft account code, RPS user token, XSTS for `http://gssv.xboxlive.com/`, the
  `xhome` login) is accepted: no device token is needed (2026-11-19).
- The console list and the play call are accepted, and a session is `Provisioned` on a Series X, with
  `nanoVersion: "V3;WebrtcTransport.dll"`. The older `"V3;WebrtcPath"` made the console fail every session with
  `0xC0840005` (`StartStreamingSessionV2Command failed`, state `ServerStartStreamingV2CommandSent`), at any tier.
- **1440p60.** With `osName: "tizen"` and a 2560x1440 display in the device info, the console's answer offers
  `a=imageattr:<pt> send [x=[0:2560],y=[0:1440],fps=[0:60]]` on video and sound.
- The answer's codecs, from an offer of H.264 High (640033), Main (4d0033), Constrained Baseline (42e033) and AV1:
  H.264 **Main, level 4.2 (`4d002a`)** first and Constrained Baseline (`42e02a`) second, both with
  `level-asymmetry-allowed=1;packetization-mode=1`; neither High nor AV1. Sound: Opus 48 kHz stereo,
  `minptime=10;useinbandfec=1`. The data channels' SCTP line is accepted.
- **ICE connects**: with our credentials in the offer and our host candidate sent through the ICE exchange, Relay's
  own ICE agent selected a direct LAN path to the console (its host candidate, port 9002) in 39 ms; the console gave
  two candidates.
- The console's answer says `a=setup:passive` on every m-line: the client is the DTLS client and starts the handshake;
  the console sends nothing until it does.
- No `/connect` call was needed for the console to answer the offer (open clients send one with a user token to
  `{session}/connect`; whether the console offering needs it to carry media is not known yet).

## Not confirmed against a console yet

Each needs a live check (`[Trait("Category", "Live")]`) before it is trusted:

- the gamepad entry's physicality fields (sent as 0) and the report's exact size;
- the vibration report's layout;
- the client description messages after the handshake, and whether the console needs all of them;
- whether the engine's H.264 decoder (openh264) takes the console's Main-profile stream at 1440p60 as fast as the
  latency budget needs; Constrained Baseline is the fallback the console also offers;
- the tier the `tizen` OS name gives on console streaming (it is known from cloud streaming);
- **which of the two asks for 1440p:** the `tizen` OS name or the 2560x1440 display in the device info (both were
  sent when the console offered 1440p60); and whether the stream then holds 1440p under load or adapts down. Never
  settle this from browser captures: their headers and cookies carry live account tokens.
