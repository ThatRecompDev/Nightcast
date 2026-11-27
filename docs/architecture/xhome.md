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

**Sign-in by code (what the apps use).** Step 1 can also run as an OAuth device-code grant (RFC 8628), with the same
client id and scope, so nothing is pasted back and the code can be entered on a phone:

- `POST login.live.com/oauth20_connect.srf` (`client_id`, `scope`, `response_type=device_code`) answers `user_code`,
  `device_code`, `verification_uri` (`https://www.microsoft.com/link`), `interval` and `expires_in`. Checked live
  2026-11-23: an 8-letter code, a 5 s interval, 900 s to enter it.
- The app then asks `POST oauth20_token.srf` (`client_id`, `grant_type=urn:ietf:params:oauth:grant-type:device_code`,
  `device_code`) every interval. Until the code is entered the answer is a 400 with `authorization_pending`;
  `slow_down` adds 5 s to the interval; anything else ends the sign-in. Success is the same token answer as the code
  grant.
- A kept refresh token the service refuses (`invalid_grant`: revoked, a changed password, long unused) is forgotten,
  and the app asks for a new code instead of failing.

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

## Away from home

The ICE exchange is the same at home and away; only the candidates that can work differ. Relay's transport is UDP
over IPv4 (the engine's), and it sends only its host candidate, so away from home the path rests on three things:

- **The console's Teredo candidate.** A console with remote features on offers, beside its LAN host candidate, a
  candidate at a Teredo IPv6 address (`2001:0000::/32`). RFC 4380 builds that address from the console router's
  public IPv4 address and the Teredo tunnel's public port, both inverted, so `TeredoAddress` reads them back and
  `IceCandidate.ReadInto` turns the line into two server-reflexive IPv4 candidates: the public address at the
  candidate's own port (open where the router forwards it, as UPnP does) and at the tunnel's port. Open clients
  describe the same reading; which of the two ports answers is **not seen yet**.
- **Our checks open our NAT.** Relay is the controlling agent and checks first; each check leaves our NAT mapped
  toward the console, so the console's answers and checks come back through it.
- **Peer-reflexive learning.** A check from an address no candidate named (the console as its NAT shows it) is
  answered and becomes a pair of its own, checked at once (RFC 8445 7.3.1.3; four spare pairs, made up front), and a
  pair whose checks failed is checked again when the console's own check arrives through it.

- **A TURN relay (optional, the user's own).** Where none of the above gets through (strict NATs on both sides, a
  carrier's NAT), the stream relays through TURN (RFC 8656, `Phantom.Relay.WebRtc.Turn`). The service hands out no
  relay for console streaming, so it is the user's: a Cloudflare TURN key in `turn.json` (`{"keyId": …, "apiToken":
  …}`, the devices gateway's shape) beside the executable or in Nightcast's local application data folder. The
  desktop asks Cloudflare for credentials that last a day (`CloudflareTurn`), takes its plain UDP URL, and the stream
  allocates on the same socket as ICE before the exchange (at most 3 s), so our candidates carry the relayed address
  and our NAT's mapping (`typ relay`, `typ srflx`). Each console candidate is then paired twice: directly and through
  a channel on the relay (which also installs the permission; the relay refuses private addresses, and those pairs
  fail at once). Relayed checks start 1.5 s after the direct ones (`IceTiming.RelayHeadStart`), so a direct path wins
  wherever one exists; DTLS, SRTP and RTCP then go through the relay as ChannelData (4 bytes more per datagram).
  Without `turn.json` nothing is asked and nothing changes. A public release never carries the file
  (`publish-desktop.ps1 -TurnKey` copies it beside a private build only and refuses `-Release`). Cloudflare bills TURN
  traffic past 1,000 GB a month; a relayed 1440p stream is about 12.6 GB an hour. **Not seen yet**: whether the
  console takes a relayed candidate (standard ICE says it does).

The desktop's log says whether a relay was set up, which kind of candidate ICE took (`Host` at home,
`ServerReflexive` or `PeerReflexive` away) and whether directly or through the relay, never an address or a
credential. Not covered: a console without IPv6/Teredo, without a forwarded port and without a relay; and native IPv6
(needs the engine's transport to carry IPv6, an engine lane). On the console: Settings, Devices & connections, Remote
features on, power mode Sleep, and Network settings should show NAT type Open or Moderate.

## Data channels

The offer carries audio (Opus, sendrecv: game sound and chat), video (H.264, recvonly) and four data channels:

| Label | Subprotocol | Use |
|---|---|---|
| `input` | `1.0` | binary reports (below), ordered |
| `control` | `controlV1` | JSON: on open the authorization request (a fixed access key, part of the protocol); pad 0 added once the session is up (below); key-picture requests |
| `message` | `messageV1` | JSON: the handshake; after its acknowledgement the client's description (system UI, install id, orientation, touch, capabilities, size) and the un-constrain transaction; every `TransactionStart` is answered with `TransactionComplete` |
| `chat` | `chatV1` | party chat, not used yet |

## The first pad

Seen live on a Series X on 2026-11-26, from a browser client built on the same protocol:

- **Un-constrain.** After the description the client starts a transaction on the message channel, target
  `/streaming/title/constrain`, content `{"constrain":false}`; the web player sends it right after connecting. Without
  it the console keeps the running game constrained and drops every input report. The console answers with
  `TransactionComplete`.
- **Order.** The console binds the pad at its first report, and a report that comes while the game is still
  constrained leaves the pad dead for the session. Pad 0 announced on the control channel together with the
  authorization raced the console's setup of the input channel (SCTP keeps no order between streams) and left input
  dead about half the time. So pad 0 is announced only after the un-constrain's completion (or 3 s after the
  description at worst), and no report goes out before it (`PadAnnouncement`).
- **Answer.** The console answers a pad it took with server metadata on the input channel (`u16 kind` 16). Without
  that answer within 1.5 s the pad is removed and announced again, at most four times.

## Input reports

Little-endian. Header: `u16 kind`, `u32 sequence`, `f64 milliseconds` (14 bytes). Client metadata adds `u8` most
touch points. A gamepad report adds `u8 count` and per pad `u8 index`, `u16 buttons`, `i16` LX LY RX RY (+Y up),
`u16` LT RT, `u32` physical and virtual physicality (23 bytes). The console's reports carry no sequence or time: its
vibration report (13 bytes, seen live 2026-11-26) is `u16 kind (128)`, `u8 type` (0, four motors), `u8 pad`, four
`u8` motor percentages (low, high, left trigger, right trigger), `u16 duration`, `u16 delay`, `u8 repeat`; its server
metadata starts with `u16 kind (16)`.

## Bitrate feedback

On 2026-11-19 the console sent about 14 Mbit/s of 2560x1440 at 60 fps H.264 over a wired LAN (round trip about
0.5 ms, no loss), to an offer whose video asks only for `nack` and `nack pli`, with no bandwidth line and no header
extension, and a client whose only RTCP is the PLI. A WebRTC sender sets its target bitrate from the receiver's
feedback; with none it may stay at its starting rate. To find out whether the console sends more when told it may,
Relay can give it the feedback a WebRTC sender expects, as three independent switches (`BitrateFeedback`), all off by
default so nothing changes until a live run shows which helps. The probe and the desktop take the same arguments
(`BitrateFeedbackArguments`); the stream logs what was offered and what the answer kept.

| Switch | Standard | On the wire |
|---|---|---|
| `--bandwidth-line <kbit/s>` | RFC 4566 (`b=AS`), RFC 3890 (`b=TIAS`) | in the video section after `c=`: `b=AS:<kbit/s>` and `b=TIAS:<bit/s>` |
| `--remb <kbit/s>` | draft-alvestrand-rmcat-remb-03 | `a=rtcp-fb:<pt> goog-remb` on every video payload; an SRTCP PSFB FMT=15 packet (`8F CE`, length, our SSRC, media SSRC 0, `REMB`, the SSRC count (8 bits), exponent (6) and mantissa (18) of the bitrate, the video's SSRC) as soon as the video's SSRC is known, then once a second |
| `--twcc` | draft-holmer-rmcat-transport-wide-cc-extensions-01, RFC 8285 | `a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01` on sound and video and `a=rtcp-fb:<pt> transport-cc` on every payload (Opus included); every 50 ms an SRTCP RTPFB FMT=15 packet (`8F CD`, length, our SSRC, the media SSRC, base sequence, status count, 24-bit reference time in 64 ms and 8-bit feedback count, run-length and one- or two-bit status vector chunks, receive deltas in 250 µs, one octet for 0 to 63.75 ms, else two signed, zero padding to a word) |

How the transport feedback is built (`TransportFeedbackRecorder`): the transport-wide sequence number is read from
each decrypted packet's header extension in either form (one-byte `0xBEDE` or two-byte `0x100x`) under the id the
**answer** gives (none echoed, none read and none sent); its arrival time is the injected clock's when the network
thread handles the datagram, so the deltas carry the receive loop's batching. A feedback covers every packet since the
last; a loss after the last received packet waits for the next feedback, a late packet already reported lost is not
reported again, a delta beyond 16 signed bits starts the next feedback, and one feedback stays within 1200 octets. The
padding is zeros without the P bit, as the draft writes it.

The received bitrate is the RTP payload (without RTP header and SRTP tag) a second, video and sound apart:
`StreamStats.VideoMegabitsPerSecond` and `AudioMegabitsPerSecond`, in the stats line, the block on screen and the
window title when it fits; the probe prints it every second and the average, peak and average access unit at the end.

**Live result (2026-11-19, the owner's Series X, GTA VI at 1440p60, wired LAN, probe runs of 30 s each):**

| Switches | Video Mbit/s (avg / peak) | Average access unit | Loss, keyframe requests |
|---|---|---|---|
| none | 10.8 / 12.7 | 22.8 KB | 0, 1 |
| `--remb 5000` | 3.7 / 5.6, and the console dropped to **1280x720** | 7.8 KB | 0, 1 |
| `--remb 80000` | 26.9 / 29.4 | 56.6 KB | 0, 1 |
| `--bandwidth-line 80000` | 10.7 / 11.6 (no `b=` echoed: ignored) | 22.5 KB | 0, 1 |
| `--twcc` | 27.1 / 29.6 | 57.2 KB | 0, 1 |
| `--remb 200000` | 26.6 / 29.4 | 59.4 KB | 301 packets, 16 |
| all three | 27.9 / 29.9 | 58.9 KB | 0, 1 |

The answer keeps `goog-remb`, `transport-cc` and the transport-wide extension (id 3, as offered); the console's
sender follows REMB both ways (its resolution too) and runs its own controller on transport feedback. The service's
ceiling for this tier is about 30 Mbit/s. The desktop now defaults to transport-cc alone (`BitrateFeedback.Recommended`:
it adapts, where a fixed REMB claim does not); `--no-feedback` restores the old ~11 Mbit/s.

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
- **DTLS connects**: Relay's DTLS client (BouncyCastle, a per-session ECDSA P-256 certificate whose fingerprint the
  offer carries) completes the handshake with the console in about 73 ms: cipher suite
  TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 (0xC02B), SRTP profile **AEAD_AES_128_GCM**, the console's certificate
  matching its answer's fingerprint. No RTP arrives yet: the console waits for the data channels (the control
  channel's authorization and the message channel's handshake) before it streams.
- **The stream**: once the four data channels are open (the control authorization and pad 0, the message
  handshake and client description, the input metadata), the console streams: the first RTP packet about 200 ms
  later, then about 60 video frames a second at **2560x1440** (H.264 Main), Opus sound and RTCP, all decrypted with
  the AEAD-AES-128-GCM keys from DTLS. It answers a PLI with a keyframe. Its messages on the message channel include
  `/streaming/properties/titleinfo` (the running game) and `/streaming/touchcontrols/showlayoutv2`.
- Software decoding (the engine's openh264) of that stream: 8.1 ms a frame on average, 41.5 ms at worst
  (keyframes), on the owner's desktop CPU. It keeps up with 60 fps, but the keyframe spikes cost latency, so a
  hardware decoder is the next latency step.
- No `/connect` call was needed for the console to answer the offer (open clients send one with a user token to
  `{session}/connect`; whether the console offering needs it to carry media is not known yet).

## Not confirmed against a console yet

Each needs a live check (`[Trait("Category", "Live")]`) before it is trusted:

- **streaming away from home** ("Away from home" above): that the console offers a Teredo candidate, which of its
  two IPv4 readings answers, whether a phone hotspot (carrier NAT) connects, and whether the console takes our
  relayed candidate through Cloudflare's TURN;
- the gamepad entry's physicality fields (sent as 0) and the report's exact size;
- the `/streaming/properties/titleinfo` message's content: `TitleInfo` reads a `titleId` field of it in any letter case,
  as a string or a number, whether the content is JSON text or an object; the field's real name and form are not seen
  yet;
- whether a pad other than pad 0 needs its own `gamepadChanged` on the control channel before its first report (the
  stream sends one);
- the client description messages after the handshake, and whether the console needs all of them;
- whether the engine's H.264 decoder (openh264) takes the console's Main-profile stream at 1440p60 as fast as the
  latency budget needs; Constrained Baseline is the fallback the console also offers;
- the tier the `tizen` OS name gives on console streaming (it is known from cloud streaming);
- **which of the two asks for 1440p:** the `tizen` OS name or the 2560x1440 display in the device info (both were
  sent when the console offered 1440p60); and whether the stream then holds 1440p under load or adapts down. Never
  settle this from browser captures: their headers and cookies carry live account tokens.
