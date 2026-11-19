# The live probe

`Phantom.Relay.Tools.Probe` checks the Xbox layer against the real service and the player's own console before any
WebRTC exists. It signs in, lists the consoles, starts a session, waits until the session takes an offer, sends a
probe offer, shows what the console answers, and always stops the session it started.

```
dotnet run --project src/Phantom.Relay.Tools.Probe -- [P720|P1080|P1080High|P1440] [--sign-out]
```

The tier defaults to the default profile's (1440p). `--sign-out` forgets the kept account first.

## What it keeps and prints

- The Microsoft account tokens go to the user's own store: `phantom.relay` under the local application data
  (`UserStorage`, the engine's desktop store). Nothing goes into the repository.
- It prints the console names, the session states and the answer's media lines (codecs, formats, bandwidth, picture
  attributes). It never prints a token, a credential, a fingerprint or an ICE candidate.

## The probe offer

A well-formed offer with nothing behind it: random ICE credentials, a random DTLS fingerprint, Opus sendrecv, video
recvonly and the data channels' SCTP line, bundled. Video offers every codec the engine decodes, best first: H.264
High, Main and Constrained Baseline at level 5.1 (enough for 1440p60), then AV1. The answer shows which one the
console picks and with what parameters. No connection is made, so the console gives up on it and the probe stops
the session.

## Reading the result

| Result | Meaning |
|---|---|
| consoles listed, `Provisioned`, an answer with a video codec | the sign-in chain, the REST calls and the offer format are right; WebRTC is next |
| sign-in refused at XSTS or at the streaming login | the plain account chain is not enough; the device-token (SISU) flow comes before WebRTC |
| no console | remote features are off on the console |
| the session fails while provisioning | the console is off, busy, or remote play is refused for this account |
| the answer names a lower profile or no 1440p hint | how 1440p is asked for is still open (xhome.md) |
