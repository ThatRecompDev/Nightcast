# How we work

Phantom.Relay adopts Phantom.Engine's rulebook in full: the engine's `docs/how-we-work.md`, through the same
packages (`Phantom.Engine.Build` for the build policy, `Phantom.Engine.Architecture` for the architecture rules). It
outranks every other document, habit and default. This page adds only what is specific to Relay.

## 1. What this repository holds

| Path | Holds |
|---|---|
| `src/Phantom.Relay.*` | the projects of the layers in [the overview](architecture/overview.md) |
| `tests` | the tests of each project and the architecture rules (`Phantom.Relay.Architecture.Tests`) |
| `scripts/test` | `test-fast.ps1`, the per-commit check |
| `docs` | this page and the architecture guides |

Never in this repository: account tokens or refresh tokens, captures of a real streaming session, console or server
ids, gamertags, user hashes, e-mail addresses, absolute paths, or anything a Microsoft service sent that identifies an
account. Tests build their payloads by hand from the shapes in the guides.

## 1a. Private builds, private source

The owner's decision of 2026-11-23, changed on 2026-11-26: the desktop app is released as **Nightcast**, as built
exes on a GitHub repository that holds only a README and its releases (`scripts/publish/publish-desktop.ps1
-Release`). Since 2026-11-26 that repository is private, for the owner alone, and a build no longer updates itself:
each build is copied to its device by hand. Since 2026-11-26 the source is pushed to that same private repository
(`origin`), only when the owner asks; nothing else of it goes anywhere. A release carries nothing of the owner's: no token, credential, console id,
gamertag, e-mail, user name or path of the owner's machines (the build maps its paths away, and the binaries are
scanned before upload). Third-party software the app can use at run time (Lossless Scaling's frame generation and
upscalers, NVIDIA's Optical Flow SDK for FRUC) is never in the repository or a release: each user's own install is
found at run time (Lossless Scaling through Steam or a configured folder). Nightcast uses no Xbox name or branding
and says it is unofficial.

## 2. The engine comes first

Relay is an engine app, the way a game is. Before writing anything, search the engine. What any app could use goes
into the engine, through the engine coordinator, as an engine lane with its own prerelease version; Relay pins that
version in `Directory.Packages.props` in a commit of its own. What stays here is only what is the console's:

- the account sign-in chain and the streaming service's REST calls ([xhome](architecture/xhome.md));
- the console's data-channel messages (control, message, input reports) and their byte layouts;
- the stream profiles, which name the console's buttons and stream tiers;
- the screens of the app.

## 3. The streaming protocol

The service's endpoints and payloads are not documented by Microsoft. They were learned from the public web player's
traffic and from what open clients describe; no code of another repository is copied. So:

- every endpoint, header and constant lives in `XboxEndpoints` or a payload record, once, with a comment saying what
  it is;
- every payload is a record with System.Text.Json source generation (`RelayJsonContext`), never a hand-built string
  and never reflection;
- every byte layout (input reports, rumble) is written as named offsets and pinned by a test that writes and reads a
  hand-made example;
- a value that was guessed rather than seen says so in its comment ("not seen yet"), and the guide lists it until a
  live check confirms it;
- a live check against a real console is a `[Trait("Category", "Live")]` test, never part of the fast suite.

## 4. Commits

As in the engine: `Area: what changed`, as `ThatMapleDev`, signed, no AI attribution, small commits of one shape,
`scripts/test/test-fast.ps1` green before each. Commit and push only when the owner asks.
