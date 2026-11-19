// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// The desktop's hardware decoder (Media Foundation on DXVA) with its pictures left on the GPU. It decodes as the
/// decoder does and reads pictures back until the renderer offers its adapter (<see cref="Offer"/>). Then, before its
/// next access unit, the decode thread moves the decoder onto that adapter when it is another (only before the first
/// picture: a new decoder needs a key frame) and from then on copies each picture on the GPU into a ring of shared NV12
/// textures (<see cref="SharedSurfaceRing"/>) and publishes it (<see cref="GpuPictureSlots"/>) for the renderer, which
/// opens the ring once. When anything of it fails, the pictures go on being read back, and <see cref="Report"/> says
/// why. The ring is made again when the stream's size changes; an old ring is kept until the decoder is disposed, since
/// the renderer may still be opening its handles.
/// </summary>
internal sealed class SharedPictureDecoder : IVideoDecoder, IGpuPictureProducer, IGpuPictureSource
{
    /// <summary>A triple buffer: one slot written, one waiting, one on screen.</summary>
    private const int RingSlots = 3;

    private const int OneGroup = 1;

    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly GpuPictureSlots _slots = new(RingSlots, OneGroup, new NewestPicturePacer());
    private readonly List<SharedSurfaceRing> _retired = [];
    private MediaFoundationH264Decoder _decoder;
    private SharedSurfaceRing? _ring;
    private GpuPictureRing? _published;
    private GpuPictureReport _report = GpuPictureReport.Idle;
    private long _offered;
    private bool _offerTaken;
    private string? _withdrawn;
    private bool _decodedAny;
    private bool _sharing;
    private int _generation;

    /// <summary>The <paramref name="decoder"/> (a hardware one; this takes it over and disposes it) and its log.</summary>
    public SharedPictureDecoder(MediaFoundationH264Decoder decoder, TimeProvider clock, ILogger log)
    {
        _decoder = decoder;
        _clock = clock;
        _log = log;
    }

    public string CodecId => _decoder.CodecId;

    public string LastError => _decoder.LastError;

    public bool Reorders => _decoder.Reorders;

    public bool IsSharing => _sharing;

    public GpuPictureRing? Ring => Volatile.Read(ref _published);

    public GpuPictureReport Report => Volatile.Read(ref _report);

    public void Offer(long adapterLuid)
    {
        Interlocked.CompareExchange(ref _offered, adapterLuid, 0);
    }

    public void Withdraw(string reason)
    {
        Volatile.Write(ref _withdrawn, reason);
    }

    [HotPath]
    public void Prepare()
    {
        var withdrawn = Volatile.Read(ref _withdrawn);

        if (withdrawn is not null && _sharing)
        {
            StopSharing(withdrawn);
        }

        var offered = Interlocked.Read(ref _offered);

        if (offered == 0 || _offerTaken)
        {
            return;
        }

        _offerTaken = true;
        TakeOffer(offered);
    }

    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha)
    {
        return Decode(frame, alpha, 0);
    }

    [HotPath]
    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha, long timestampNanoseconds)
    {
        _decodedAny = true;

        return _decoder.Decode(frame, timestampNanoseconds);
    }

    public bool TakePicture(VideoFrame target)
    {
        return _decoder.TakePicture(target);
    }

    public bool TakePicture(VideoFrame target, out long timestampNanoseconds)
    {
        return _decoder.TakePicture(target, out timestampNanoseconds);
    }

    [HotPath]
    public bool Publish(long decodedAt, out int width, out int height)
    {
        width = _decoder.PictureWidth;
        height = _decoder.PictureHeight;

        if (!_decoder.TryTakeSurface(out var surface, out var slice) || !EnsureRing(surface))
        {
            return false;
        }

        if (!_slots.TryClaim(out var slot, out var release))
        {
            return false;
        }

        if (!_ring!.WaitForReader(release.Fence, release.Value))
        {
            _slots.Abandon(slot);
            StopSharing("the decoder's device could not open the renderer's fence");

            return false;
        }

        _ring.Copy(slot, surface, slice);
        var ready = _ring.Signal();
        _slots.Add(new GpuPicture(_generation, slot, ready, width, height, _decoder.PictureColour, decodedAt, false));
        _slots.Publish();

        return true;
    }

    public bool TryTake(out GpuPicture picture)
    {
        return _slots.TryTake(out picture);
    }

    public void Release(in GpuPicture picture, nint fence, ulong value)
    {
        _slots.Release(picture, new GpuSlotRelease(fence, value));
    }

    public void Flush()
    {
        _decoder.Flush();
    }

    public void Dispose()
    {
        _decoder.Dispose();
        _ring?.Dispose();

        foreach (var ring in _retired)
        {
            ring.Dispose();
        }

        _retired.Clear();
    }

    // The renderer's adapter: the decoder moves there unless it is there already, then shares.
    private void TakeOffer(long adapterLuid)
    {
        if (!_decoder.IsHardwareAccelerated)
        {
            StopSharing("the decoder runs in software");

            return;
        }

        if (_decoder.AdapterLuid != adapterLuid && !TryMove(adapterLuid))
        {
            return;
        }

        _sharing = true;
        Volatile.Write(ref _report, _report with { Path = GpuPictureReport.ZeroCopy });
    }

    private bool TryMove(long adapterLuid)
    {
        if (_decodedAny)
        {
            StopSharing("the stream started before the renderer offered its GPU");

            return false;
        }

        var moved = MediaFoundationH264Decoder.TryOpen(_clock, preferHardware: true, adapterLuid, out var problem);

        if (moved is null || !moved.IsHardwareAccelerated)
        {
            StopSharing($"the decoder could not move to the renderer's GPU: {problem ?? moved?.FallbackReason}");
            moved?.Dispose();

            return false;
        }

        _decoder.Dispose();
        _decoder = moved;
        _log.MovedToAdapter(adapterLuid);

        return true;
    }

    // A ring the size of the decoder's surfaces: made at the first picture, and again when the stream's size changes.
    private bool EnsureRing(nint surface)
    {
        if (_ring is not null && _ring.Fits(surface))
        {
            return true;
        }

        var ring = SharedSurfaceRing.TryCreateFor(_decoder.Device, RingSlots, surface, out var problem);

        if (ring is null)
        {
            StopSharing(problem ?? "the shared textures could not be made");

            return false;
        }

        if (_ring is not null)
        {
            _retired.Add(_ring);
        }

        _ring = ring;
        Share(ring);

        return true;
    }

    private void Share(SharedSurfaceRing ring)
    {
        _generation++;
        _slots.Reset(_generation);

        var handles = ImmutableArray.CreateBuilder<nint>(ring.Count);

        for (var slot = 0; slot < ring.Count; slot++)
        {
            handles.Add(ring.Handle(slot));
        }

        Volatile.Write(ref _published, new GpuPictureRing(_generation, handles.MoveToImmutable(), ring.FenceHandle, ring.Width, ring.Height));
        _log.Sharing(ring.Count, ring.Width, ring.Height);
    }

    private void StopSharing(string reason)
    {
        _sharing = false;
        Volatile.Write(ref _report, _report with { Path = $"{GpuPictureReport.ReadBack}: {reason}" });
        _log.ReadBack(reason);
    }
}
