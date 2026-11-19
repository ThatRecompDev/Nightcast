# Lossless Scaling's upscalers in Relay

`Phantom.Relay.Lsfg.Upscaling` runs Lossless Scaling's own upscalers (LS1, its performance setting, SGSR) on the
hardware decoder's D3D11 device, so the owner can compare them with the engine's FSR 1 live. The conditions of
[lsfg.md](lsfg.md#conditions-binding) hold unchanged: nothing of Lossless Scaling is in this repository (no DLL, no
shader bytes, no dumps, no disassembly listings); `Lossless.dll` is read at run time from the owner's own install, and
what the analysis found is committed only as the ids, shapes and constant meanings below, each checked against the DLL
when an upscaler is built. The numbers in this page that came from looking at how a shader computes are marked
**inferred**; those a test settles are marked **measured**.

## What the other shaders are

The 2025-09 `Lossless.dll` holds 300 raw-data resources: 202 DXBC compute shaders (ids 101 to 302) and 98 others (303
to 400, not DXBC; not looked at further). Ids 255 to 302 are LSFG 3 ([lsfg.md](lsfg.md)). The DLL's run-time type names
(its RTTI) name the effects it carries: `Anime4k_S/M/L/VL/UL`, `Bicubic_CAS`, `FSR`, `LS1`, `LS1_P`, `NIS`,
`SharpBilinear`, `SinglePass`, `LSFG`, `LSFG2`, `LSFG3`; the app's own enums (`LosslessScaling.dll` metadata) list the
scaling types `LS1, FSR, NIS, SGSR, BicubicCAS, Anime4K, XBR, SharpBilinear, Integer, Nearest` and `LS1Type`
`BALANCED, PERFORMANCE`. Reflection (thread groups, bindings, constant-buffer fields) and how each shader samples and
stores grouped the rest:

| Ids | Group, bindings | What it is |
|---|---|---|
| 101-132 | 8 x 8, `InputTexture*`, `UpdateMapTexture` (a changed-region mask), one to fifteen feature inputs | Anime4K's five sizes (inferred from the RTTI and the shapes) |
| 133 | 8 x 8, picture to output, a sampler, `InputConsts1/2`, `OutputConsts` | Catmull-Rom bicubic in five bilinear taps, crop-aware (inferred) |
| 134 | 64 x 1, `const1`, `OutputConsts` | AMD CAS (sharpen only): with 133, `Bicubic_CAS` (inferred) |
| 135 | 16 x 16, `OutputConsts` only | the same bicubic without a crop (inferred) |
| 136, 241, 254 | five instructions | copies with an offset |
| 137-140 | 64 x 1, `samLinearClamp`, `Const0..3` | FSR 1's EASU variants (inferred; the engine has its own FSR 1) |
| 247-248 | 64 x 1, `Const0` | FSR 1's RCAS (inferred) |
| **141-145** | 16 x 16, picture to a one-channel output | **LS1 performance**: one network each (below) |
| **146** | 16 x 16, `EffectTexture` (1 channel) at t0, `InputTexture` at t1, a sampler | **LS1's combine** (below) |
| **147-161** | 16 x 16, five sets of three steps | **LS1**: five networks of three steps (below) |
| 162-181 | `MipInvDimension`, `FirstIteration`, flows and masks | LSFG 1 (inferred) |
| 182-240, 242 | the same names, three sets | LSFG 2 (inferred) |
| 243-246 | 256 and 128 threads, `coef_scaler`, `coef_usm` tables, `kDetectRatio`... | NVIDIA NIS (its constant names; it needs its coefficient tables, so it is not wired) |
| 249 | 8 x 8, one bilinear sample | bilinear / nearest by sampler (inferred) |
| **250** | 8 x 8, `gather4` of green, a 8/255 edge threshold | **SGSR** (Snapdragon GSR; below) |
| 251-252 | `PreviousFrameTexture`, `UpdateMapTexture` | Anime4K's changed-region map |
| 253 | 8 x 8, luma weights 10.2, 34.3, 3.5 | xBR (inferred; the engine has its own xBR) |

## The constant buffer

Every upscaling shader binds one 48-byte buffer of three uint4 fields (reflected names and offsets, checked per shader
by `UpscaleShape`): `InputConsts1` at 0, `InputConsts2` at 16, `OutputConsts` at 32. Their meaning, read from how the
shaders use them (**inferred**, then **measured** to give a correct picture): `InputConsts1` = the picture's width,
height, then its texture's width and height (the combine divides by them for its UVs; the networks stop two texels
before the width and height); `InputConsts2` = the picture's origin in its texture (the networks add it to the thread
position, wrapping as a signed shift), then an option: LS1's networks treat a non-zero `.z` as an scRGB picture (they
then take `(c / 7.5)^(1/2.2)` before luma: HDR, not driven; SGSR reads `.z` as its sharpness, edge sharpness
`1 + 0.2 z`); `OutputConsts` = the output's width and height, then where it starts in the output texture.

## The LS1 pipeline

| Pass | Ids | Reads | Writes |
|---|---|---|---|
| features | 147 + 3k | the picture: BT.601 luma of a 3 x 3 neighbourhood | four features at the picture's size |
| refine | 148 + 3k | the features, a 2 x 2 neighbourhood (CReLU, stored biased by 0.5) | four features at the picture's size |
| detail | 149 + 3k | the refined features, 2 x 2 | four luma values per texel, stored as a 2 x 2 block at twice the size (one channel) |
| combine | 146 | the detail at the output pixel's UV (bilinear), the picture bilinear at the output pixel | the picture's luma replaced by its luma plus the detail, chroma kept (BT.601), alpha 1 |

`k` is the network, 0 to 4. LS1's performance setting (`LS1_P`) replaces the three steps by one pass, 141 + k (3 x 3
luma to the 2 x 2 detail block), then the same combine. So the networks always work at twice the picture's size and
the combine scales to any output size; chroma is upscaled bilinearly. The features are stored biased by 0.5 in
R8G8B8A8_UNORM (its clamp is the network's activation), the detail in R16_FLOAT (it is signed).

**Measured** (`LosslessUpscalerLiveTests.The_open_choices_measured`, the clean chart, luma PSNR against the 2160p
truth): the networks read at the picture's origin, both of them (one texel either way costs 3.5 dB: 17.2 against 20.9
for LS1, 17.1 against 20.7 for its performance setting); 8-bit features measure 0.1 to 0.35 dB better than 16-bit
float ones. The five networks differ only in how much detail they add: edge contrast rises with `k` on every picture
(below), so they are LS1's strength steps; which value of Lossless Scaling's own `LS1Sharpness` setting picks which
network is not known (**inferred** that it does). Relay uses network 2 unless F7 changes it: on clean pictures its edge
contrast matched the truth's (0.99 to 1.02).

## In Relay

- `LosslessUpscaler`: one upscaler at one geometry (input size, output size, output texture size), its shaders checked
  and created from the mapped DLL, its textures and constant buffers made once; `Upscale(rgba)` only binds and
  dispatches (a picture's shader view is made once per texture and kept). `LosslessUpscaleStage` adds the decoder's
  NV12 to RGBA and back to NV12 (the video processor, as for LSFG) and a GPU timer.
- On the decoder's GPU path (`Desktop/Pictures/UpscaledPictures`, `SharedPictureDecoder`): the renderer asks for the
  pictures at the letterbox's size (`IGpuPictureSource.Scale(PictureScale)`: the method, the size, LS1's network); the
  decoder makes its shared ring at that size (NV12 needs even sizes: 3600 x 2025 goes in slots of 3600 x 2026, the
  picture in their top-left corner) and upscales every picture it publishes into it. Order: decode, LSFG at 1440p
  (when on), then the upscaler for each generated frame and the decoded picture, then the screen; the renderer draws
  the picture one to one, the engine adds no upscale. Without the GPU path, or when the DLL is missing or a step is
  refused, the reason is logged and shown, and the pictures come at 1440p, which the renderer upscales with FSR 1.
- In the app (`StreamPicture`, `ScaleRequest`): a new method or network is asked at once, a new window size once it
  has held for 20 frames (a drag does not remake the decoder's ring at every step); the last two old rings are kept for
  the renderer, older ones let go. F6 steps FSR 1, LS1, LS1 performance, SGSR, xBR, off; F7 steps FSR 1's RCAS
  sharpness 0.2, 0.1, 0, 0.5 stops, or with LS1 its network 2, 3, 4, 0, 1. The stats read `LS1 (network 2) → 3600x2025
  0.21 ms`. The profile's default stays FSR 1.

## Measured on the owner's RTX 4090 (2026-11-20)

Test pictures (`UpscaleTestImages`, drawn from continuous functions, each pixel its footprint's average): a chart
(wedges, small glyphs, a zone plate, thin lines, checkerboards) and a natural texture (fractal noise, grass, pebbles);
the 1440p picture clean, softened (3 x 3 binomial, an encoder's smoothing) or blocky (8 x 8 DCT at JPEG quality 55).
Each went the stream's way (NV12, BT.709 limited) and was upscaled to 3840 x 2160; luma PSNR against the 2160p truth,
and edge contrast (mean luma gradient) against the truth's. FSR 1 and bilinear through the engine's device
(`UpscaleComparisonLiveTests`); the engine's bilinear matched a CPU bilinear of the converted picture to 0.03 dB.

| Picture | bilinear | FSR 1, RCAS 0.2 | FSR 1, RCAS 0 | LS1 net 0 | LS1 net 2 | LS1 net 4 | LS1 perf net 2 | SGSR |
|---|---|---|---|---|---|---|---|---|
| chart, clean | 19.38 / 0.59 | 20.42 / 0.90 | 20.30 / 0.96 | 20.95 / 0.83 | 20.92 / 0.99 | 20.42 / 1.14 | 20.71 / 1.06 | 20.71 / 0.80 |
| chart, soft | 16.09 / 0.29 | 16.92 / 0.41 | 17.20 / 0.46 | 16.62 / 0.33 | 16.98 / 0.36 | 17.34 / 0.40 | 16.96 / 0.37 | 16.75 / 0.34 |
| chart, blocky | 19.22 / 0.64 | 20.01 / 0.99 | 19.85 / 1.07 | 20.48 / 0.90 | 20.35 / 1.08 | 19.84 / 1.23 | 20.21 / 1.14 | 20.15 / 0.91 |
| natural, clean | 43.55 / 0.76 | 39.50 / 1.37 | 36.34 / 1.74 | 45.23 / 0.94 | 44.52 / 1.02 | 42.64 / 1.12 | 44.31 / 1.03 | 42.89 / 0.89 |
| natural, soft | 38.45 / 0.55 | 39.59 / 0.82 | 39.20 / 1.04 | 39.40 / 0.61 | 39.70 / 0.62 | 39.76 / 0.64 | 39.77 / 0.65 | 38.90 / 0.58 |
| natural, blocky | 38.13 / 0.72 | 35.66 / 1.20 | 34.07 / 1.51 | 37.60 / 0.86 | 37.16 / 0.92 | 36.57 / 0.98 | 37.24 / 0.93 | 37.41 / 0.83 |

(PSNR in dB / edge contrast as a fraction of the truth's.) What it says: LS1 is the most faithful upscaler on clean
and blocky pictures (closest to the truth, edges about as strong as the truth's), where FSR 1 oversharpens fine
texture (contrast 1.4 to 1.7 times the truth's on the natural picture, 4 to 7 dB below LS1) and amplifies block edges.
On a softened picture LS1 adds little (0.62 against bilinear's 0.55; network 4: 0.64), where FSR 1's RCAS restores
more contrast (0.82 at 0.2, 1.04 at 0): **if the stream reaches Relay soft, LS1 will look softer than FSR 1**, not
sharper; LS1's network 4 on blocky pictures rings visibly along thin lines. SGSR sits between bilinear and LS1.

GPU time, 2560 x 1440 in (each step waited for, so at full clocks): LS1 0.215 ms to 3840 x 2160 (0.210 to 3600 x
2025), LS1 performance 0.114 (0.110), SGSR 0.074 (0.066); FSR 1 through the engine 0.224 ms for its three passes. The
decoder path's whole step, NV12 to RGBA, LS1, RGBA to NV12 into a 3600 x 2026 slot, queued back to back: 0.61 ms of
wall time a picture (the video processor's two conversions run on its own engine, outside the 3D queue's timestamps).
The decoder end to end (`SharedPictureDecoderLiveTests`, a hardware-decoded synthetic 1440p stream): pictures handed
over at 3600 x 2025 in a 3600 x 2026 ring, with and without LSFG x2, and at 1440p again once the request is withdrawn.

The stats' GPU time brackets a picture's whole step on the 3D queue, so, as with LSFG
([lsfg.md](lsfg.md#why-a-live-stream-showed-4-to-5-ms-2026-11-19)), it includes the waits for the video engine and grows
when the GPU clocks down under a light load (4.3 ms measured unpaced in the end-to-end test against 0.6 ms throughput).

## Still to confirm live

- How LS1 looks on the real stream against FSR 1 (the softened-picture rows above predict LS1 looks less sharp unless
  the stream is clean), and which network the owner prefers.
- The stats' upscale time during a real stream (clock-dependent, above), and that a window resize settles without a
  visible hitch (each settled size remakes the upscaler and the ring: about 0.25 s on the decode thread, as LSFG's).
- That LSFG and LS1 together keep pace at 60 pictures a second (measured separately: about 0.8 ms and 0.6 ms).
- Which of Lossless Scaling's `LS1Sharpness` values selects which network.
