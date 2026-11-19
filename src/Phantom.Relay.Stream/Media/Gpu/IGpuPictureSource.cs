// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Profiles;

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// The render thread's side of a decoder whose pictures stay on the GPU: the renderer offers its device's adapter
/// once (<see cref="Offer"/>); from then on, where the decoder can share with it, the pictures arrive in the slots of
/// <see cref="Ring"/>, which the renderer opens once, and each refresh it takes the picture to show
/// (<see cref="TryTake"/>), draws it inside its own access to the slot, and says what that read ends at
/// (<see cref="Release"/>) so the decoder waits for it, on the GPU, before it writes the slot again. Until a picture
/// comes this way the CPU path (<see cref="PictureMailbox"/>) carries them. Game- and platform-agnostic: on Windows the
/// handles are DXGI NT handles of D3D11 textures and a D3D11 fence.
/// </summary>
public interface IGpuPictureSource
{
    /// <summary>The newest ring, once the decoder shares; null before.</summary>
    GpuPictureRing? Ring { get; }

    /// <summary>The path and frame generation as they stand, for the stats.</summary>
    GpuPictureReport Report { get; }

    /// <summary>
    /// The renderer can open shared textures of the adapter of <paramref name="adapterLuid"/>: the decoder moves to that
    /// adapter (before its first picture) and shares its pictures from then on, or says in <see cref="Report"/> why not.
    /// </summary>
    void Offer(long adapterLuid);

    /// <summary>
    /// Frame generation as the running game's profile sets it, from the next new picture on. It runs on the GPU path
    /// only, where the platform has it; <see cref="Report"/> says what it does or why it is off.
    /// </summary>
    void Use(FrameGeneration settings);

    /// <summary>
    /// Upscales the pictures where they are decoded, as <paramref name="scale"/> asks, from the next picture on (the
    /// ring is then made at that size), or hands them over at their own size again (<see cref="PictureScale.None"/>).
    /// It runs on the GPU path only, where the platform has the method; <see cref="Report"/> says what it does or why not.
    /// </summary>
    void Scale(PictureScale scale);

    /// <summary>The renderer cannot take the pictures on the GPU after all (it refused the ring): the decoder reads them back from its next picture on.</summary>
    void Withdraw(string reason);

    /// <summary>The picture this refresh shows; false to keep the one on screen.</summary>
    bool TryTake(out GpuPicture picture);

    /// <summary>The renderer's read of <paramref name="picture"/> this frame ends when its fence <paramref name="fence"/> reaches <paramref name="value"/>.</summary>
    void Release(in GpuPicture picture, nint fence, ulong value);
}
