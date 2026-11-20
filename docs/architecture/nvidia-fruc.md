# NVIDIA FRUC

NVIDIA's Frame Rate Up-Conversion (FRUC, the `NvOFFRUC` library of the Optical Flow SDK) is Relay's second frame
generator, beside Lossless Scaling's ([lsfg](lsfg.md)). It runs on the optical flow accelerator and CUDA of a Turing or
later NVIDIA GPU, on the decoder's D3D11 device, in the same place of the GPU picture path as LSFG; F4 switches between
them live. It lives in the optional, Windows-only `Phantom.Relay.Nvidia`.

## The owner's condition

Personal use only, never released. Nothing of the SDK is in this repository: no DLL, no header, no document, no
sample. The C# declarations of the five entry points and their structures are written by hand from the header
(`Interop/`, every number in `FrucValues`), and the library is loaded at run time from the owner's own SDK folder.

## How it is loaded

`NvidiaFrucLocator` finds the folder holding `NvOFFRUC.dll`: the folder `PHANTOM_NVOFFRUC_DIR` names (or the
library's own path), else the newest `Optical_Flow_SDK_*` folder's `NvOFFRUC\NvOFFRUCSample\bin\win64` at the root of
a fixed drive, in the user's folder or in their Downloads (the desktop names those roots, `NvidiaSdkFolder`; no path is
stored). `FrucLibrary` loads `cudart64_110.dll` from that folder first, by its full path, then `NvOFFRUC.dll`, both
with `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32`: their imports come from their own folder and
System32, never from the current directory or PATH. The five exports are taken as function pointers. The SDK's sample
also checks the library's NVIDIA signature (WinVerifyTrust); Relay does not, since the folder is the owner's own.

Before anything is loaded, the decoder's device must be on an NVIDIA GPU (DXGI's vendor id 0x10DE): a PC with an
integrated GPU has two adapters.

## The API flow

1. `NvOFFRUCCreate`: the frame size (2560 x 1440), the decoder's `ID3D11Device`, `DirectX11Resource`, the surface
   format, no CUDA resource type.
2. The generator makes its own textures, since FRUC reads and writes only registered ones: three outputs (x4's frames)
   and two inputs used in turn (FRUC keeps the previous frame by reference), RGBA, shared with NT handles
   (`MISC_SHARED | SHARED_NTHANDLE`, for CUDA's D3D11 interop), and a shared `ID3D11Fence`.
3. `NvOFFRUCRegisterResource`, once: the five textures (outputs first, as the sample lists them) and the fence.
4. Per new picture: the decoded NV12 surface to RGBA on the video processor (`Nv12ToRgbaConverter`, as LSFG), copied
   into the next input, then `NvOFFRUCProcess` with the input's time stamp (one per frame, from 1) and the output's
   (the pair's time plus k/M). For x3 and x4 the same input is passed again with the next time stamps, each into its
   own output. Each output goes back to NV12 into a slot of the shared ring (`RgbaToNv12Converter`).
5. `NvOFFRUCUnregisterResource` with the same five, then `NvOFFRUCDestroy`, when the decoder goes.

`FrucGenerator` is the binding, `FrucFrameStage` the stage behind LSFG's seam (`IFrameGenerationStage` in
`Phantom.Relay.Lsfg`), `GeneratedPictures` (desktop) holds either.

## Synchronisation

The guide allows an `ID3D11Fence` (Windows 10 1703 and later) or keyed mutexes; Relay uses the fence, which never
blocks a CPU thread on D3D's side. Per picture: the device's queue copies the new frame into the input and sets the
fence to v (`Signal`, then `Flush` so CUDA sees it); FRUC is told to wait for v before it reads and to set v + 1 once
the output is written (`uSyncWait.FenceWaitValue`, `uSyncSignal.FenceSignalValue`), and v + 2 and so on for x3 and
x4's further calls; the device's queue then waits for the last value (`Wait`) before anything later: the conversions of
the outputs, the next copy. The input written for the next picture is the other one, so FRUC's cached previous frame
is never overwritten while it is read.

But `NvOFFRUCProcess` itself holds the calling thread until its work is done (a CUDA stream synchronise inside): the
decode thread spends about 4 ms per generated frame in it when frames come back to back, and about three times that at
the stream's pace (the GPU idles between pictures): 12, 21 and 30 ms a new picture at x2, x3 and x4 in
`QuadrupledDecodeLiveTests` (30 new pictures a second, LS1 on or off alike). x4 falls behind the 33 ms between new
pictures, so x4 asked of FRUC runs x3, and the stats say so (`NVIDIA FRUC x3 → 90 fps, x4 capped: NVIDIA FRUC keeps
up to x3`).

## What the library allows (measured, SDK 5.0.7, driver 617.14)

- **One FRUC per process at a time.** A second instance alive beside the first breaks the first's synchronisation
  (its CUDA acquire fails, `cudaErrorInvalidValue`) or crashes the process. The live tests run in one collection.
- **One FRUC per D3D11 device for the device's life.** Once one is destroyed, the next on the same device cannot share
  its fence (`NvOFFRUCRegisterResource` returns invalid handle; FRUC's log: "failed to create shared handle for fence
  object"); a fresh device works, again and again. So the desktop makes the decoder's FRUC the first time it is asked
  for and keeps it, paused (`FrucGenerator.Restart`), while frame generation is off or runs LSFG, and destroys it only
  with the decoder or on a new picture size (`NvidiaFrucCache`); a size change on the same device then falls back.
- **NV12 is unusable**: its output's luma is right but its chroma plane is left unwritten (a green picture). RGBA works,
  so FRUC runs on the video processor's RGBA, as LSFG does.
- **Several frames per pair work**, though the guide says not to call Process twice with one frame: the same frame
  passed again with a later output time gives that frame exactly (x4's frames at a quarter, a half and three quarters,
  each within 0.04 luma levels of the ideal), and the next pair's frame is right after it. Each such call is a full
  run (about 4 ms), and FRUC writes a line to its own log for each ("Execution is out of order"), so x3 (the most the
  stream runs, below) adds a line a picture: some 3.5 KB a second at a 30 fps source.
- FRUC writes its errors to `%ProgramData%\NVIDIA Corporation\NvOFFRUC\logFRUCError.txt`; it is the first place to
  look when a call returns `NvOFFRUC_ERR_GENERIC`. That file grows while x3 runs; it may be deleted.

## Measurements

On the owner's RTX 4090 (2026-11-20, `FrucArtifactLiveTests` and `FrucCostLiveTests`, LSFG's third-person case:
a still textured head-sized ellipse over a textured background moving 16, 32 or 64 px a frame at 2560 x 1440). Mean
luma error to the ideal middle frame (0 to 255), in the band around the foreground's edge and over the whole frame:

| Background moves | NVIDIA FRUC (RGBA) edge / whole | LSFG x2 quality edge / whole | plain blend edge / whole |
|---|---|---|---|
| 16 px | 0.50 / 0.02 | 0.62 / 0.08 | 2.03 / 3.41 |
| 32 px | 0.36 / 0.03 | 1.24 / 0.26 | 7.18 / 11.98 |
| 64 px | 0.32 / 0.05 | 2.14 / 0.92 | 15.03 / 22.63 |

FRUC's NV12 path scored 15 to 16 / 11 (its missing chroma); the NV12 round trip itself costs 0.3. FRUC never gave the
previous frame back ("frame repetition") on these scenes. To the eye (crops written with `FRUC_ARTIFACT_DIR` set):
FRUC leaves a thin, broken fringe of dragged background texels right on the head's outline; LSFG a soft halo of
smeared background some 50 px wide around it. The case is pure translation, which optical flow handles best; real
game motion (parallax, particles, HUD) is for a live run to judge.

Cost per new picture (GPU from the copy in to the device's queue passing FRUC's fence; FRUC's call holds the CPU as
long):

| | x2 | x3 | x4 |
|---|---|---|---|
| NVIDIA FRUC, GPU | 4.26 ms | 8.28 ms | 11.99 ms |
| NVIDIA FRUC, CPU in the call | 3.89 ms | 7.92 ms | 11.63 ms |
| LSFG quality, GPU | 0.75 ms | 1.52 ms | 1.76 ms |
| LSFG quality, CPU in the call | 0.10 ms | 0.14 ms | 0.18 ms |

So FRUC is the sharper generator on this case and LSFG the far cheaper one; FRUC costs a few ms of the decode
thread per picture (12 ms at x2 at the stream's pace), LSFG almost none (1.2 ms at x4 with LS1). In the desktop's live test (`FrucPicturesLiveTests`, a synthetic stream through
the real decoder) the stage's GPU time, conversions included and the first pictures counted, read 7 to 10 ms at x2.

## Switching and falling back

F4 cycles off → LSFG → NVIDIA FRUC → off (`FrameGenerationSwitch`); F5 steps LSFG's settings (and turns LSFG on). A
profile chooses with `FrameGeneration.Generator` (`Lsfg`, the default, or `NvidiaFruc`); a profile kept before it
existed runs LSFG. The F3 stats and the short title show `NVIDIA FRUC x2 4.3 ms`, `LSFG x2 (quality, flow 1.00) 0.8 ms`
or `frame generation off`. FRUC asked for but missing (no SDK folder, not an NVIDIA GPU, a call refused) or failing
while it runs logs why once and LSFG runs instead (`LSFG x2 (…) instead of NVIDIA FRUC`); without LSFG either, the
stream plays without frame generation.

## Not known yet

Only a live run against the console can say how FRUC's frames look in GTA (HUD, particles, fast camera turns), whether
its 4 ms per picture on the decode thread ever delays a decode at the stream's real cadence, and how FRUC's CUDA work
shares the GPU with the decoder and the renderer at 60 fps.
