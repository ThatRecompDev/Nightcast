# LSFG: Lossless Scaling's frame generation in Relay

`Phantom.Relay.Lsfg` runs Lossless Scaling's frame generation (LSFG) on the hardware decoder's D3D11 device, to show
a 30 fps game at 60 (or 90, 120). It is optional, Windows-only and off by default; a profile turns it on, and it runs
on the decoder's GPU picture path (see "Integration" below).

## Conditions (binding)

The owner owns Lossless Scaling and has the developer's consent for personal use; Relay is personal use only and
never released ([how we work, 1a](../how-we-work.md)). On that basis:

- **Nothing of Lossless Scaling is in this repository**: no DLL, no extracted shader, no dumped constants or weights,
  no captures, no disassembly. Relay reads `Lossless.dll` from the user's own install at run time.
- The code is written from public descriptions of the pipeline and from what Windows' own APIs report about the
  shaders (D3DReflect). No code of any other port is used: lsfg-vk's licence (CC BY-NC-ND) forbids derivatives.
- Reading the user's DLL at run time (`LoadLibraryEx` with `LOAD_LIBRARY_AS_DATAFILE`, `FindResource`,
  `LoadResource`) and analysing it to derive the pass graph is done under the owner's permission. What the analysis
  found is committed only as the code that wires the passes and the few numbers it needs (resource ids, constant
  offsets), each commented with how it was found; every one of them is checked against the DLL when the pipeline is
  built.
- Shaders are never compiled at run time. The NV12 to RGBA step uses the GPU's video processor, not a shader.

## Loading at run time

1. `LosslessLocator.Find(settings.LibraryPath, LosslessLocator.SteamLibraries())`: the configured path when the file
   is there, else `steamapps/common/Lossless Scaling/Lossless.dll` in each Steam library that Steam's
   `libraryfolders.vdf` lists (Steam's install folder comes from `HKCU\Software\Valve\Steam`). No path is stored here.
2. `LsfgShaderCatalog.TryLoad`: maps the file as data (none of its code runs), enumerates its `RT_RCDATA` resources,
   and reflects each DXBC container with `D3DReflect`: program type, thread-group size, bindings (name, kind, slot,
   component count) and the fields of constant buffer 0 (name, offset, size, read or not). It logs only ids, kinds and
   binding counts, never bytes or paths. The DLL of 2025-09 holds 300 raw-data resources, 202 of them compute shaders.
3. `LsfgGenerator.TryCreate(device, catalog, settings, width, height, format, log)`: picks the quality or performance
   set, checks every role's shape and constant layout against the reflection, creates the compute shaders from the
   mapped bytes (`CreateComputeShader`), allocates the textures and records every dispatch. The catalog may be
   disposed afterwards.

## The pipeline as implemented

Ids 255 to 302 were confirmed by reflection to be the frame-generation set: 255 and 256 shared, 257 to 279 the quality
set, 280 to 302 the performance set, one for one 23 ids apart (same roles and wiring, half the feature textures).
`LsfgShaderIds` names each role; `LsfgShapes` checks that each step reads exactly what the step before wrote plus the
extra inputs listed below. All shaders bind one constant buffer layout (`LsfgConstants`, offsets checked per shader),
at most two samplers (both bilinear, clamped) and run 8x8 thread groups unless noted.

Per real frame (34 dispatches):

| Pass | Ids | Reads | Writes |
|---|---|---|---|
| mipmaps | 255 (32x32 groups, a 2x2 block per thread) | the frame | 7 single-channel luma levels, from the flow size (frame size x flow scale) halving |
| alpha, on each of the 7 levels | 267-270 | the level | 4 feature textures at a quarter of the level (steps 1 and 3 halve) into a ring of 3 frames |
| beta | 275-279 (279: 32x32 groups) | the finest level's features of the last 3 frames, oldest first | 6 single-channel UI-mask levels (reads `UiThreshold` = 0.5) |

Per generated frame (66 dispatches), with `Timestamp` = (index + 1) / (count + 1):

| Pass | Ids | Reads | Writes |
|---|---|---|---|
| gamma, each flow level 0 (coarsest) to 6 | 257, then 259-262 | both frames' features at the level, the coarser level's flow (none at level 0: `FirstIter`), the mask level two finer than the level's size (measured, see "Artifact review") | the level's flow (forward in xy, backward in zw) |
| delta flow, levels 4 to 6 | 257, then 263-266 | as gamma, the prior being the previous iteration's second flow (gamma level 3's flow on the first) | a second flow candidate |
| delta blend, levels 4 to 6 | 258, then 271-274 | both frames' features, the coarser gamma flow, the previous second flow and weights (none on the first: `FirstIterS`) | the blend weights |
| generate | 256 (16x16 groups) | previous frame, current frame, finest flow, finest second flow, weights | the generated frame (R8G8B8A8) |

Texture formats: four-channel features are R8G8B8A8_UNORM, whose clamp to [0, 1] on store is the networks'
activation (their steps end in a scale and bias with no activation of their own); flows and weights are
R16G16B16A16_FLOAT (signed); luma and mask levels are R8_UNORM. `ResolutionInvScale` is the frame width over the
flow width; `AdvancedColorKind` and `HdrSupport` are 0 (SDR).

How the roles were found: the reflected input and output counts and component widths chain uniquely (alpha 1-2-2-4-4,
beta 12-2-2-2-2-6, a correlation of 2x4 features plus one prior to 3, its chain 3-4-4-4 plus prior and mask to 1); the
constants each reads (`FirstIter`, `FirstIterS`, `Timestamp`, `UiThreshold`); and how each samples its inputs (at
twice the thread position for the halving steps, displaced by a flow times `Timestamp` or `1 - Timestamp` for the
warped ones).

## Verified live (2026-11-19, the owner's RTX 4090)

`LsfgGeneratorLiveTests` builds the pipeline at 2560x1440 on a hardware device and generates the frame between two
synthetic frames (a textured gradient moved 32 px and brightened). In a probe at 1280x720 the finest flow measured +-16 px
for a 32 px move (the expected half motion, both directions, both modes), and the generated frame's mean difference to the ideal middle frame was 1.4 levels in
quality mode and 0.3 in performance mode, against 11 for a plain blend. GPU time:

| Mode, flow scale | Per real frame | Per generated frame |
|---|---|---|
| quality, 1.0 | 0.23 ms | 0.52 ms |
| quality, 0.5 | 0.13 ms | 0.31 ms |
| performance, 1.0 | 0.16 ms | 0.30 ms |

## Still hypotheses (not settled by a test)

- **Beta's frame order**: the 12 inputs are fed oldest frame first. A swap would change only the UI mask; measured
  below, it makes no difference worth the name, so the order stays.
- **Delta's priors**: the first delta iteration's flow chain starting from gamma level 3's flow, and the blend
  correlation's two flow inputs being the coarser level's gamma flow and the previous second flow. The second flow
  measured right, so the flow chain's wiring holds; the blend inputs are less certain (not measured yet).
- **The mask level** per flow level: two levels finer than the level's size since the artifact review (below), which
  measured it; what Lossless Scaling itself binds is not known.
- **Samplers**: both bound as bilinear clamp; point sampling measured far worse (below). Lossless Scaling's own sampler
  states are not known.
- **HDR**: `AdvancedColorKind` 2 and `HdrSupport` are not driven; only SDR frames are supported.

## Artifact review (2026-11-19)

The owner saw artifacts around the head of GTA VI's character: a still foreground over a background the turning camera
moves. `LsfgArtifactLiveTests` renders that case at 1440p: a textured ellipse of 400 x 540 px (head and shoulders)
still in the middle, a textured background moving 16, 32 or 64 px a frame; three frames in, the frame between the last
two out, compared with the ideal middle frame (background moved 1.5 steps). The mean luma error is taken in the band
around the edge (as wide as two steps, at least 32 px: where background is uncovered and covered), inside the
foreground, and in the background beyond the band; each of the graph's unsettled choices (`LsfgWiring`) is run. With
`LSFG_ARTIFACT_DIR` set it writes 800 x 800 crops (generated, ideal, difference x4) as PNG. Edge band error, x2 quality:

| Wiring | 16 px | 32 px | 64 px | foreground | background (32 px) |
|---|---|---|---|---|---|
| mask of the same size (before) | 0.72 | 1.54 | 2.65 | 0.00 | 0.22 |
| mask one level coarser | 0.85 | 1.82 | 3.44 | 0.00 | 0.23 |
| mask one level finer | 0.64 | 1.33 | 2.30 | 0.00 | 0.21 |
| **mask two levels finer (now)** | **0.62** | **1.24** | **2.14** | **0.00** | **0.21** |
| the finest mask everywhere | 0.67 | 1.24 | 2.15 | 0.00 | 0.21 |
| no mask at all | 0.73 | 1.39 | 2.06 | 0.03 | 0.20 |
| beta newest first (mask two finer) | 0.63 | 1.31 | 2.13 | 0.00 | 0.21 |
| point samplers (mask two finer) | 1.93 | 3.46 | 9.94 | 0.00 | 7.76 |
| a plain 50/50 blend, for scale | 2.03 | 7.18 | 15.03 | 0.00 | 12.57 |

What it shows: the UI mask is what keeps a still region exact (without it the foreground itself picks up error), but
read at the coarse flow levels at their own size it also holds the background still in a wide, blocky halo around the
still foreground (the difference image shows it reaching some 100 px out at 32 px a frame). A finer mask level keeps
the held region tight: two levels finer cuts the edge error by 14 to 19 % at every speed, with the foreground still
exact and the background no worse, so it is adopted (`LsfgWiring.FinerMask`). Coarser is worse, finer still (the finest
everywhere) gains nothing more. Beta's order makes no consistent difference (oldest first kept). Point sampling breaks
the flow (the background error rises 35-fold): bilinear stays. What is left at the edge is the interpolation's own
disocclusion error, a few levels at most. Only a live run says whether this is the artifact the owner saw.

## NV12 to RGBA

`Nv12ToRgbaConverter` turns the decoder's NV12 surfaces (slices of its texture array) into one R8G8B8A8 texture with
`ID3D11VideoContext::VideoProcessorBlt`: BT.709 limited range in, full-range RGB out, fixed-function, no shader. Input
views are made once per surface and kept. A video processor refuses an NV12 texture bound for shader reads only; the
decoder's surfaces are bound as decoder outputs, which it accepts. Checked live on limited-range black and white.

## Pacing

`FramePacer` (pure, on an injected clock) says what each display refresh shows. When real frame n arrives at A, with
the pair's source interval I and multiplier M, generated step k is due at A + (k - 1) I / M and real frame n at
A + (M - 1) I / M. Each refresh shows the latest due slot that is ready (within half a refresh), never going back, so
a generated frame shows as soon as it is ready, a late one is skipped rather than shown out of order, and a real frame
is never held back a whole source frame. A new real frame drops what was pending of the pair before.

I is the real time between the pair's two pictures: the difference of their access units' RTP timestamps (90 kHz,
handed through `IGpuPictureProducer.Publish`), so a game dipping to 25 fps spreads that pair over 50 ms and the
delay from source to screen stays the same within it. It is capped at two nominal intervals (66.7 ms), so after a
long hold the real picture is due before the next one is likely to come; a gap over four nominal intervals is a pause
and falls back to the arrivals' smoothed interval, as does a pair without timestamps. The display's refresh (for the
half-refresh tolerance) is not known to the engine's window API, so the pacer measures it from its own refresh calls
(the render thread's cadence: what the pictures actually meet), starting from 60 Hz and smoothing; a gap over 25 ms is
a stall and does not count.

## Settings

`LsfgSettings`: `Enabled` (false), `Multiplier` 2 to 4 (2), `FlowScale` 0.25 to 1.0 (1.0; lower is faster),
`PerformanceMode` (false), `LibraryPath` (empty: found through Steam). `Normalized()` brings them into range.

## Interop

Source-generated COM only (`GeneratedComInterface`, `LibraryImport`), NativeAOT-safe. The D3D11 definitions are shared
with the decoder, one per mechanic, in `Phantom.Relay.Media.Windows/Interop` (`ID3D11Device` and
`ID3D11DeviceContext` extended in header order, the view, sampler, buffer and query descriptions, `ID3D11VideoDevice`,
`ID3D11VideoContext`, `Direct3D11Values`); Media.Windows lets `Phantom.Relay.Lsfg` see them (`InternalsVisibleTo`).
The shader reflection (`ID3D11ShaderReflection`, and its constant-buffer and variable objects, which are not COM
objects and are called through function pointers) and the resource loading are Lsfg's own.

## Integration

The desktop head runs it (the Heads layer may reference Lsfg; `Pictures/SharedPictureDecoder`,
`Pictures/GeneratedPictures`, `Pictures/LsfgPicturePacer`), on the decoder's GPU picture path
([overview](overview.md#the-gpu-picture-path)):

- **Settings.** The running game's profile (`StreamProfile.FrameGeneration`: off, or the quality or performance
  shaders, x2 to x4, a flow scale of 0.25 to 1.0; off by default, the GTA VI preset at x2 quality) reaches the decode
  thread through `IGpuPictureSource.Use`, and the stage is made again when it or the picture's size changes.
- **Only new pictures.** The console sends 60 units a second and repeats each picture of a 30 fps game;
  `RepeatClassifier` tells them apart (overview, "Frame generation"). A new picture is converted to RGBA
  (`Nv12ToRgbaConverter`, straight from the decoder's surface) and submitted, and the frames between it and the one
  before are generated; a repeat is decoded (it is a reference) but not published.
- **One way to the screen.** Each generated frame is turned back into NV12 by the video processor
  (`RgbaToNv12Converter`: full-range RGB in, BT.709 limited out) into a slot of the decoder's shared ring, beside the
  decoded picture, so generated and decoded pictures are drawn alike by the engine's NV12 picture renderer. (The engine
  draws an imported RGBA texture only through its canvas, which blends by alpha and the generator's alpha is not known
  to be opaque; NV12 also keeps the decoded picture bit-exact.) The ring then holds 2 x 4 + 1 slots: a group being
  written, a group waiting and the picture on screen.
- **Pacing.** A new picture and the frames before it are published as one group; `FramePacer` (behind the slots'
  `IPicturePacer` port) says at each refresh which of them shows: the generated ones from the new picture's arrival on,
  the new picture itself (M - 1) / M of a source interval later.
- **When it cannot run** (Lossless Scaling not installed, shaders that do not fit, a ring the video processor cannot
  write, the pictures read back), it logs why once and the stream plays without it. The F3 stats show
  `LSFG x2 (quality, flow 1.00) 0.8 ms` (the GPU time of a new picture's whole work) or `LSFG off: <reason>`.
- **Live switching.** F4 turns it off or on again for the running stream, F5 steps through x2 quality, x2
  performance, x2 quality at flow 0.5 and x2 performance at flow 0.5 (`FrameGenerationSwitch`, the profile's
  multiplier kept); a new title starts from its profile again. The choice goes through `IGpuPictureSource.Use` like the
  profile's, so the decode thread makes the new stage before its next new picture (a quarter of a second, the decode
  thread's alone: the render thread never waits, the stream catches up after it). With the stats hidden the window's
  title shows the choice (`Phantom.Relay | frame generation x2 performance, flow 0.50`).

Measured on the owner's RTX 4090 (2026-11-19, `LsfgFrameStageLiveTests` and a window drawing a synthetic 1440p
30 fps stream through the engine's device): x2 quality at full flow costs 0.83 ms of GPU a new picture (both colour
conversions, the 34 passes of the real frame, the 66 of the generated one) and 0.8 ms of the decode thread's CPU to
queue it. Decode to present averaged 10.4 ms (median 7.0, worst 15.3) against 5.2 ms without it: generated frames show
as soon as they are made, the new picture half a source interval later, as designed. Making the stage (reading the DLL,
reflecting its shaders, building the pipeline) takes about a quarter of a second, once, on the decode thread, when a
profile turns it on: a single hitch as the game starts.

### Why a live stream showed 4 to 5 ms (2026-11-19)

A live GTA VI run showed 4 to 5 ms in the stats where the benchmark gave 0.83 ms. `LsfgCostLiveTests` reproduces the
stream's path on the decoder's own device: a 1440p H.264 stream (Windows' encoder, at test time) decoded by the
hardware decoder, every other unit a new picture through the stage's steps with a GPU timestamp after each:

| Run | NV12 to RGBA | real frame's passes | generated frame's passes | RGBA to NV12 | stage bracket |
|---|---|---|---|---|---|
| back to back, bracket right after the decode (as the stream) | 0.000 | 0.244 | 0.532 | 0.007 | 0.78 ms |
| back to back, bracket once the decode is done | 0.000 | 0.246 | 0.519 | 0.007 | 0.77 ms |
| a unit every 16 ms (the console's rate), 2 s | 0.000 | 0.298 | 0.658 | 0.007 | 0.96 ms |
| a unit every 16 ms, 10 s, per second | | | | | 0.78, 1.8, 5.7, 6.3, 11.5 ms |

So none of the suspects: the decode is not inside the bracket (opening it after the decode is done changes nothing;
the decode itself takes about 2 ms of the video engine, never counted), the conversions run on the video processor and
cost the 3D queue nothing measurable (their wait shows in the real frame's first copy), the decoder hands out 6
surfaces whose input views are made once, nothing is created per picture, and nothing maps or waits on the CPU. It is
the shaders' own work, at the GPU's clock: `nvidia-smi` beside the 10-second run shows the RTX 4090 at P0 (2535 MHz)
for the first second and then stepping down to P5 and P8 (435 to 210 MHz) under a load this light (a millisecond
every 33), the bracket growing in step (0.78 ms at 2535 MHz, 11.5 ms at 210 MHz: the clock ratio), and a 32 ms stall
when it climbs back. The live stream's 4 to 5 ms is the same pass work at an intermediate state (the renderer keeps
some load on the GPU). Nothing in Relay is wrong; the fix is the driver's power policy for the app: NVIDIA Control
Panel, Manage 3D settings, Program settings, `Phantom.Relay.Desktop.exe`, Power management mode "Prefer maximum
performance" (only a live run confirms the stats then read about 0.8 ms; it also shortens the decode and the
present). Relay does not keep the GPU busy on purpose, nor lock its clocks (`SetStablePowerState` needs developer mode
and pins the base clock, below boost).

Not settled until a live run with a 30 fps game: the pacing of a dipping game from the RTP timestamps (pinned by
tests on a manual clock only) and the held-picture threshold (synthetic sizes; the second refinement's real size is
not measured yet).