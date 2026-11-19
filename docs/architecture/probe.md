# The live probe

`Phantom.Relay.Tools.Probe` checks the Xbox layer against the real service and the player's own console before any
WebRTC exists. It signs in, lists the consoles, starts a session, waits until the session takes an offer, sends a
probe offer, shows what the console answers, and always stops the session it started.

```
dotnet run --project src/Phantom.Relay.Tools.Probe -- [P720|P1080|P1080High|P1440] [--view] [--seconds <n>]
    [--bandwidth-line <kbit/s>] [--remb <kbit/s>] [--twcc] [--sign-out]
```

The tier defaults to the default profile's (1440p). `--sign-out` forgets the kept account first. `--seconds` keeps a
measuring run for n seconds instead of six. The bitrate feedback switches (xhome.md, "Bitrate feedback") are off
without their arguments and combine freely; the desktop app takes the same ones.

## What it keeps and prints

- The Microsoft account tokens go to the user's own store: `phantom.relay` under the local application data
  (`UserStorage`, the engine's desktop store). Nothing goes into the repository.
- It prints the console names, the session states and the answer's media lines (codecs, formats, bandwidth, header
  extensions, RTCP feedback, picture attributes) and which of the offered bitrate feedback the answer kept. While the
  stream runs it prints the video and sound bitrate once a second; at the end the video's average and peak Mbit/s,
  the average access unit and the REMB and transport feedback packets sent. It never prints a token, a credential, a
  fingerprint or an ICE candidate.

## The offer and the stream

The probe sends Stream's real offer (`StreamOffer`): the session's ICE credentials and DTLS certificate fingerprint,
Opus sendrecv, video recvonly with H.264 High, Main and Constrained Baseline at level 5.1 (enough for 1440p60) and AV1,
and the data channels' SCTP line, bundled. It then pumps Stream's `ConsoleLink` (ICE, DTLS, the channels and the media
path) phase by phase with a deadline each and reports what each phase measured; the decoding is Stream's
`DecodeWorker` with the engine's `H264Decoder`. A measuring run keeps the stream six seconds; `--view` shows it in a
window until the window closes. The app streams through the same pieces (`StreamConnection`), so what the probe
measures is what the app runs.

## Reading the result

| Result | Meaning |
|---|---|
| consoles listed, `Provisioned`, an answer with a video codec | the sign-in chain, the REST calls and the offer format are right; WebRTC is next |
| sign-in refused at XSTS or at the streaming login | the plain account chain is not enough; the device-token (SISU) flow comes before WebRTC |
| no console | remote features are off on the console |
| the session fails while provisioning | the console is off, busy, or remote play is refused for this account |
| the answer names a lower profile or no 1440p hint | how 1440p is asked for is still open (xhome.md) |
