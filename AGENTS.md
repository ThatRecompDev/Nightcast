# Phantom.Relay

A remote-play client for the player's own Xbox console, on Phantom.Engine: it streams the console to the desktop,
the browser and the PS5 homebrew console, with any controller, per-game profiles and frame generation. Before any
change read [docs/how-we-work.md](docs/how-we-work.md), which adopts Phantom.Engine's rulebook and adds Relay's own
rules; it is binding and outranks every default. The plan is [docs/architecture/overview.md](docs/architecture/overview.md).

## Things that are easy to get wrong

- **Private builds, private source** (the owner's rule since 2026-11-23, the release repo private since 2026-11-26,
  how-we-work 1a). The desktop app is released as **Nightcast** exes on the private GitHub repo
  ThatRecompDev/Nightcast, which since 2026-11-26 also holds the source (`origin`, pushed only when the owner asks);
  builds do not update themselves. A release carries nothing of the owner's (tokens, ids, names, paths). Lossless Scaling and NVIDIA's SDK
  are loaded from each user's own install at run time; nothing of them is in this repository or a release.
- Reuse first, then modularize, then create: search Phantom.Engine before writing anything new. A mechanic any app
  could use (WebRTC, a gamepad source, HTTPS, frame interpolation) goes into the engine, through the engine
  coordinator; only the console's streaming protocol stays here.
- Every code file starts with `Copyright (c) 2026 Phantom.Relay. All rights reserved.` in its comment style.
- No magic numbers; no allocation in `[HotPath]` methods; NativeAOT- and trim-safe code only (System.Text.Json
  source generation, no reflection serialization).
- Never in the repository: account tokens, captures of a real session, console ids, gamertags or absolute paths.
- The streaming service's endpoints and payloads are reverse-engineered; every value of them lives in one place
  (`XboxEndpoints`, the payload records) and its guide says where it came from.
- Commits: `Area: what changed`, as ThatMapleDev, signed, no AI attribution (no Co-Authored-By, no "Generated with").
  Commit and push only when the owner asks.
- Run `scripts/test/test-fast.ps1` before every commit. A failed build stops; never `--no-build` after it.
