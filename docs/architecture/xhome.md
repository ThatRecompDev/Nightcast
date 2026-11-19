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
| `POST v5/sessions/home/play` | start: server id, settings (`nanoVersion: "V3;WebrtcPath"`, `osName` android = 720p, windows = 1080p, tizen = 1080p high; 1440p: see below) |
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
- The console list and the play call are accepted, and a session reaches `Provisioning` on the console.

## Not confirmed against a console yet

Each needs a live check (`[Trait("Category", "Live")]`) before it is trusted:

- the gamepad entry's physicality fields (sent as 0) and the report's exact size;
- the vibration report's layout;
- the client description messages after the handshake, and whether the console needs all of them;
- whether the console starts streaming for our play request: on the first live probe (2026-11-19, a Series X that
  was on) the session went `Provisioning` then `Failed` with the console's own error `0xC0840005`
  (`StartStreamingSessionV2Command failed`, state `ServerStartStreamingV2CommandSent`) at both 1440p and 1080p. One
  public report of the same state was cured by power-cycling the console; whether our request differs from the
  official client's is open until the official client is checked on the same console;
- the tier the `tizen` OS name gives on console streaming (it is known from cloud streaming);
- **how a client asks for 1440p.** Remote play gained 1440p at higher bitrates in 2026 (first for Insiders; the
  console must output 1440p or 4K, and "Auto" quality picks the highest the link allows). The web client's code names
  the tiers 720, 720HQ, 1080, 1080HQ and 1440, but the play request's field for them has not been seen yet. Until it
  is, `P1440` sends the highest known OS name and a 1440p display description. To settle it: start remote play on
  xbox.com with 1440p chosen, read the play request in the browser's developer tools, and write the field here
  (never the capture itself).
