# PlayStation pads on Windows

`Phantom.Relay.Pads.Windows` reads the DualSense, the DualSense Edge and the DualShock 4 on Windows, over USB and
Bluetooth, through the system's HID API, for the engine's gamepad ports. The engine's desktop platform reads only
Xbox-protocol pads (XInput); this is the source its hosting guide says a PlayStation pad still lacks. It is
game-agnostic and knows nothing of the console, so it can move into the engine's `Platform.Desktop` later.

## Public API

| Type | What it does |
|---|---|
| `PlayStationPads` | `IGamepadSource`, `IGamepadFeedback`, `IGamepadFamilies` and `IDisposable`. `Start()` begins discovery, `Stop()` ends every thread and closes every pad. `Poll` copies each slot's newest state (allocation-free, measured). `TrySetRumble`, `TrySetLight` leave feedback for the pad's reader. `Family` answers `PlayStation` for a slot with a pad. `TryGetMotion(slot, out PadMotion)` is the motion side channel. |
| `Reports.PadMotion` | the gyroscope's and accelerometer's raw counts, the pad's sensor time and a sequence that grows with each report; the factory calibration is not applied yet |
| `Reports.*InputReport`, `*OutputReport`, `PadInputReports`, `PadOutputReports` | the byte layouts, as named offsets, each pinned by a test on hand-made bytes |
| `Reports.BluetoothOutputCrc` | the CRC-32 of the `0xA2` header byte and the report, appended little-endian (the engine's `Crc32`) |
| `Reports.PlayStationProducts` | Sony's product ids: DualSense `0x0CE6`, Edge `0x0DF2`, DualShock 4 `0x05C4` and `0x09CC` |

## How it runs

- **Discovery** (its own thread, every 2 s): the configuration manager lists the HID interfaces. Each new path is
  identified once through a handle opened without access (`HidD_GetAttributes`). A device that is not a Sony pad is
  remembered while it is present, so it is asked only once. A pad goes into the first free slot of four. A pad
  another program holds open is tried again at the next look.
- **Reading** (a thread per pad): one overlapped `ReadFile` is always pending, and the reader waits on its event and
  a wake event together. Each full report is parsed into the slot under a lock held only for the copy. A read that
  fails means the pad is gone, so the reader empties its slot, closes the pad and ends.
- **Transport**: a pad whose longest input report is 64 bytes is on USB, anything longer is on Bluetooth. Over
  Bluetooth the pad first sends a short report. Reading the calibration feature report once (`0x05` DualSense,
  `0x02` DualShock 4) switches it to the full report (`0x31`, `0x11`).
- **Feedback**: `TrySetRumble`/`TrySetLight` store the request in the slot and wake the reader. The reader writes
  it between reads: DualSense `0x02` (USB) or `0x31` (Bluetooth, with a sequence tag and the CRC); DualShock 4
  `0x05` or `0x11`. The first light report also ends the DualSense's own light-bar animation. The frame never waits
  on a write.

## Mapping

Cross, circle, square and triangle are South, East, West and North. L1/R1 are the shoulders, and L2/R2 are the
triggers' travel plus their digital bits. Create (Share) is Back, Options is Start, PS is Guide, the touchpad click
is Touchpad, and the DualSense's mute button is Misc. The hat's eight directions become the d-pad buttons. The
sticks run from -1 to 1. HID's y already points down, as the engine's does, so y is not turned. The Edge's extra
buttons (Fn, back paddles) have no engine button and are not read.

## Checked and not checked

Every layout is written from the public HID descriptors and the Linux hid-playstation documentation; no code was
copied. A live test (`Category=Live`) checked a DualSense over USB: discovery, the 64-byte report, the motion, a
48-byte output report taken by the pad, and threads that start and stop. Not yet checked on hardware:

- Bluetooth (the feature-report switch, reports `0x31`/`0x11`, the output CRC);
- the DualShock 4 and the Edge;
- whether the pad's motors actually turn;
- newer DualSense firmware's second rumble mode, which is not sent.

`Poll` uses slots 0 to 3, as `XInputGamepads` does. A head that uses both sources needs to merge them.
