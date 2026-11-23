# Screenshots, and where captures are saved

A screenshot keeps the console's own picture as a PNG. It is the desktop head's (`Phantom.Relay.Desktop/Stills`), on
Windows and Linux alike. Nightcast only saves its captures; whatever is done with them afterwards (another program
watching the folders) is not Nightcast's.

## Screenshots

F1 or the pad menu's Screenshot row (`StreamApplication`) call the head's `IScreenshots.Take`. PrintScreen is left to
the system and other programs. The picture saved is the **console's own**: the newest decoded picture at the stream's
size (2560x1440 at the 1440p tier), before any upscaler, generated frame, letterbox or overlay.

| Step | Where | Thread |
|---|---|---|
| ask for the next picture (`PictureGrab.Request`, one at a time) | `ScreenshotTaker` | render |
| copy it to the CPU (`PictureGrab.Copy`), or read it back from the GPU (`IGpuPictureProducer.TryReadBack`) | `DecodeWorker` | decode, once |
| YCbCr 4:2:0 to 8-bit RGB with the engine's rows for the picture's colour (BT.709 limited unless the stream says otherwise), as the renderer's shader does (`PictureRgb`) | `Stream/Stills` | worker |
| PNG: signature, IHDR (true colour, 8 bits), one zlib IDAT with the Sub filter on every row, IEND, CRC-32 per chunk (`PngWriter`) | `Stream/Stills` | worker |

Where the pictures come from:

- **Linux** (VA-API through libavcodec, or openh264): the decoder reads each picture back into the
  `PictureMailbox`; the decode thread copies the one being published.
- **Windows, GPU path** (`SharedPictureDecoder`): the pictures stay in D3D11 textures. After the next publish the
  decode thread copies the decoder's own output surface (the slice of its texture array, before the shared ring's copy,
  the upscaler and the frame generation) into the decoder's staging texture and maps it
  (`MediaFoundationH264Decoder.TryReadSurface`, the read-back path the CPU path already uses). It waits for the GPU
  once, on the decode thread, never on the render thread.
- **Windows, read-back path**: as on Linux.

If no picture comes within three seconds (a paused stream), the request is cancelled and the window says so. Once
saved, the window shows "Screenshot saved: <path>" for a few seconds, in the recorder's place over the picture and in
the title (`NoticeBoard`).

`PngWriter` is an engine candidate: the engine reads PNG (`Phantom.Engine.Imaging`'s `PngReader`, which the tests
decode the writer's files with) but writes none yet.

## Where captures are saved

One rule for every head (`Places/UserFolders`), each in a `Nightcast` folder:

| Capture | Windows | Linux |
|---|---|---|
| recordings, a folder each (`<local time>/`) | `Videos\Nightcast\` | `$XDG_VIDEOS_DIR`, else the user-dirs Videos, else `~/Videos`, then `Nightcast/` |
| screenshots (`<local time>.png`, ` (2)` and on within one second) | `Videos\Nightcast\` (beside the recordings) | `$XDG_PICTURES_DIR`, else the user-dirs Pictures, else `~/Pictures`, then `Nightcast/` |

As with recordings, a screenshot is a capture of a real session and never goes into the repository.

## When a capture is whole

So that a program watching these folders never takes half a file:

- a **screenshot** is written as `<name>.png.partial` and renamed to `<name>.png` once whole; a `.png` is always
  complete;
- a **recording** folder gets an empty file named `done` (`RecordingFolder.DoneFile`) once `source.mp4`, `shown.mp4`
  and `recording.txt` are whole and closed, before ffmpeg's comparison (`compare.mp4`, which may follow later). A
  folder without `done` is still recording or failed. Every head's recorder follows this rule.

**Tests.** `PngWriterTests` round-trip through the engine's reader; `PictureRgbTests` pin the conversion's greys and
red; `DecodeWorkerTests` and `PictureGrabTests` the hand-off on both paths; `ScreenshotFileTests` the names and the
folder rule; `RecordingFolderTests` the `done` marker; `PadMenuTests` the menu's row. Not tested here: the Windows
read-back of a real DXVA surface, which needs a stream.
