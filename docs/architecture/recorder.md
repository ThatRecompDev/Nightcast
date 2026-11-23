# The recorder (F9)

The recorder keeps, side by side, what the console sent and what Relay showed, so the owner can compare a game's own
30 fps (GTA VI renders 30; the console sends 60 units a second, every other one a refined repeat) with what frame
generation makes of it (60, 90 or 120 fps). It is the desktop head's (`Phantom.Relay.Desktop/Recording`), on Windows.

## What is recorded, and where

F9 during a stream starts a recording; F9 again stops it. Each recording gets a folder of its own under the user's
Videos folder (`Environment.SpecialFolder.MyVideos`): `Videos\Phantom.Relay\2026-11-20 21-14-03\` (the local time it
started; ` (2)` and on when a folder of that second exists). In it:

| File | What |
|---|---|
| `source.mp4` | the console's H.264 access units exactly as received, repeats included, remuxed into MP4 without re-encoding; times from the RTP 90 kHz clock |
| `shown.mp4` | every picture Relay put on screen, decoded and generated, at the moment the pacer showed it, at the stream's size (before the window's upscale), encoded on the GPU |
| `recording.txt` | the settings (frame generation and its multiplier, the upscaler, the labels), each file's frames, size, length and bitrate (read back from the file), what was skipped or dropped and why, and what became of the comparison |
| `compare.mp4` | where the user has ffmpeg: source left, shown right, synced, labelled `console 30 fps` and `Relay LSFG x2` |
| `compare-half-speed.mp4` | the same at half speed |

**Never in the repository.** A recording is a capture of a real session, which the repository never holds
([how we work](../how-we-work.md)); it goes to the user's own Videos folder, and nothing of Relay writes there
otherwise. recording.txt names no account, gamertag, console or path: only settings and counts. The tests make their
streams and pictures at test time and write to a folder `RELAY_RECORDER_TEST_DIR` names (else the system's temporary
folder).

## How

**Start.** F9 (`StreamApplication`) hands the head's `IStreamRecorder` what the window shows with (`RecordingSettings`:
the frame generation's description, its multiplier, the upscaler's description). The recorder (`StreamRecorder`) makes
the folder, starts a `RecordingSession` and asks the console for a key frame through the stream's `AccessUnitTap`
(a PLI on the network thread's next turn), so the files start within a few frames rather than at the next periodic key
frame. Until it comes the window shows `● REC waiting for a key frame`, then `● REC 00:42`. Nothing to record yet (no
picture decoded) refuses the start with a reason.

**source.mp4** (`SourceRecorder`). The decode worker offers each access unit to the tap before decoding it; the
listener queues the worker's own copy of it (no second copy) from the first key frame on and returns. The queue holds
256 units (about four seconds); a full queue drops the unit and counts it, and the decode thread never waits. The
recorder's own thread opens the file at the first unit (its SPS and PPS describe the stream: `Mp4H264Remuxer`, Media
Foundation's sink writer with the stream's type as both input and output, so it passes the units through) and writes
each unit with its time from the first unit's RTP timestamp (`RtpTimeline`: 90 kHz to 100 ns, counted from the first so
no rounding gathers, wrap-safe), holding each until the next gives its duration. While no key frame has come, it asks
again every second.

**shown.mp4** (`ShownRecorder`). The decoder's GPU path (`SharedPictureDecoder`) tells its `IShownPictureTap` of each new
ring of shared textures (decode thread) and of each picture the render thread takes for the screen (`TryTake`: the
pacer's choice for this refresh). While a recording runs, `ShownPictureTap` hands that slot's texture to the recorder,
which queues a GPU copy of it into a texture of the encoder's own pool (`Mp4VideoEncoder.TryCopy`: eight NV12 textures
on the decoder's device), so the shared ring is never held, and queues the slot and the moment. Nothing waits: no free
pool texture (the encoder behind) or a full queue (32) drops the picture and counts it. The first picture shown asks
the recorder's thread to make the encoder (about 0.3 s on the owner's PC, measured by `DecodedPictureRecordingLiveTests`
on the decoder's real ring; the pictures shown meanwhile are not recorded, so shown.mp4 starts that much after
source.mp4, and the comparison's offset puts both on one timeline) (Media Foundation's sink writer with hardware transforms allowed and the
decoder's device manager: on the owner's RTX 4090 `NVIDIA H.264 Encoder MFT`, NVENC), H.264 up to 4096 wide, else
HEVC; 80 Mbit/s mean (unconstrained VBR), budgeted for the shown rate (60, or 30 x the multiplier), BT.709 limited range
like the stream. Each picture's time is the moment it was shown, from the first; so the file plays at the real 60, 90 or
120 fps, a held refresh lengthens the picture before it, and a generated frame the pacer skipped is not in it. A pool
texture is free again once the encoder released its sample (its reference count read back to the pool's one).

On the CPU read-back path (software decoding, a ring the renderer refused) no picture comes this way: shown.mp4 is not
made, and recording.txt and the log say so.

**Stop.** F9 (or closing the window) lets go of both taps and finishes on a thread of its own: both queues drained, both
files finished and read back (`Mp4Reader`, the source reader: frames, key frames, size, length, codec), recording.txt
written, the window showing `saved to …\Phantom.Relay\<stamp>`. Closing the window waits, after it closed, for a
recording still being finished (the console says so). Once saved, before the comparison, the recorder writes an empty
`done` file into the folder, the mark a program watching it waits for ([screenshots](screenshots.md)).

**Comparison.** ffmpeg is the user's own, found at run time (`FfmpegLocator`: the PATH, else
`%USERPROFILE%\scoop\apps\ffmpeg\current\bin\ffmpeg.exe`), never bundled; without it the comparison is skipped with the
reason in the window, the log and recording.txt. Its arguments are built one by one (`CompareArguments`, through
`ProcessStartInfo.ArgumentList`: no shell, no quoting): the shown file started later by its offset (`-itsoffset`, the time
between the source's first key frame and the first picture shown after it, so both sides show the same moment), the
shown side scaled to the source's size, both brought to the shown rate (`fps`), labelled (`drawtext`, Consolas from the
system's fonts; labels keep only plain characters), side by side (`hstack`), and for the half-speed file the times
doubled (`setpts=2*PTS`, every frame kept). It encodes with `h264_nvenc` (preset p7, high quality, constant quality 18),
or `hevc_nvenc` when the pair is wider than the 4096 NVENC's H.264 takes, as two 1440p pictures (5120) are. Its progress
(`-progress pipe:1`) shows in the window: `saved to … | compare 42 %`, then `compare.mp4 made`.

## Costs (measured on the owner's RTX 4090, 2026-11-20)

| | Cost |
|---|---|
| Decode thread, per access unit | a queue add under a short lock; no copy, nothing allocated |
| Render thread, per shown picture | 0.07 ms average, 0.12 worst (1280x720, the copy's queueing, a flush and the queue add; `RecordingLiveTests`); the copy's call alone 0.03 ms at 1440p; nothing allocated |
| GPU, per shown picture | 0.02 ms median for the 1440p copy into the pool (`Mp4VideoEncoderLiveTests`); the encoding runs on NVENC, apart from the 3D and compute work |
| Recorder's threads | the remux about 0.3 ms a unit; the encoder's queueing; both off the stream's threads |
| Comparison | two 1280x720 seconds side by side, normal and half speed, in 1.8 s with NVENC |
| Latency | none added: the copy is queued on the decoder's device after the slot was written and the renderer never waits for it; the decoder's device is multithread-protected, so the render thread holds its lock for the copy's call only |

## Engine candidates

`Mp4H264Remuxer`, `Mp4VideoEncoder` and `Mp4Reader` (`Phantom.Relay.Media.Windows/Recording`) are game-agnostic: the
engine has an MP4 demuxer (`Media.Containers.Mp4`) and no muxer or encoder. A video recording port (encode a GPU
texture, mux a coded stream) with this Windows implementation is an engine lane, through the coordinator.

## Only a live stream confirms

- That the console answers the PLI with a key frame promptly (the recorder asks again every second until one comes).
- The source's sync with what was shown on a real link: `-itsoffset` uses the first picture shown after the first key
  frame, so the pictures' own delay (decode to present, and frame generation's half a source frame) shows as the shown
  side being that much behind, as it is on screen.
- The cost at the console's real rate with LSFG running (the render thread's and the GPU's), and that the NVENC session
  and the decoder share the GPU without a hitch.
- With Lossless Scaling's upscalers the decoder's ring is at the window's size, so shown.mp4 is too (the comparison
  scales it to the source's size).
