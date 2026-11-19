// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// The desktop's hardware decoder (Media Foundation on DXVA) with its pictures left on the GPU. It decodes as the
/// decoder does and reads pictures back until the renderer offers its adapter (<see cref="Offer"/>). Then, before its
/// next access unit, the decode thread moves the decoder onto that adapter when it is another (only before the first
/// picture: a new decoder needs a key frame) and from then on copies each picture on the GPU into a ring of shared NV12
/// textures (<see cref="SharedSurfaceRing"/>) and publishes it (<see cref="GpuPictureSlots"/>) for the renderer, which
/// opens the ring once. With frame generation on (<see cref="Use"/>), each new picture is published with the frames
/// generated before it (<see cref="GeneratedPictures"/>) and a repeat is not published at all; the frame pacer then
/// says which of them each refresh shows. Asked to upscale (<see cref="Scale"/>, Lossless Scaling's upscalers), it
/// upscales each picture it publishes to the window's size (<see cref="UpscaledPictures"/>) into a ring made at that size,
/// so the renderer draws it one to one. When anything of it fails, the pictures go on being read back (or play without
/// frame generation), and <see cref="Report"/> says why. The ring is made again when the stream's size changes; an old
/// ring is kept a while (the last <see cref="MaxRetired"/>), since the renderer may still be opening its handles.
/// </summary>
internal sealed class SharedPictureDecoder : IVideoDecoder, IGpuPictureProducer, IGpuPictureSource
{
    /// <summary>The most pictures one new picture gives: x4 frame generation.</summary>
    private const int MaxGroup = FrameGeneration.MaximumMultiplier;

    /// <summary>The group being written, the group waiting and the picture on screen (a triple buffer without generation).</summary>
    private const int RingSlots = (2 * MaxGroup) + 1;

    /// <summary>Old rings kept: the renderer opens only the newest, so two back are surely closed or never opened.</summary>
    private const int MaxRetired = 2;

    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly GpuPictureSlots _slots = new(RingSlots, MaxGroup, new NewestPicturePacer());
    private readonly List<SharedSurfaceRing> _retired = [];
    private readonly GeneratedPictures _frames;
    private readonly UpscaledPictures _upscale;
    private MediaFoundationH264Decoder _decoder;
    private SharedSurfaceRing? _ring;
    private GpuPictureRing? _published;
    private string _path = GpuPictureReport.ReadBack;
    private FrameGeneration _wanted = FrameGeneration.Off;
    private FrameGeneration? _applied;
    private PictureScale _wantedScale = PictureScale.None;
    private bool _ringUpscaled;
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
        _frames = new GeneratedPictures(clock, log);
        _upscale = new UpscaledPictures(log);
    }

    public string CodecId => _decoder.CodecId;

    public string LastError => _decoder.LastError;

    public bool Reorders => _decoder.Reorders;

    public bool IsSharing => _sharing;

    public GpuPictureRing? Ring => Volatile.Read(ref _published);

    /// <summary>The path, and what frame generation does (or why it is off: it needs the GPU path).</summary>
    public GpuPictureReport Report
    {
        get
        {
            var generation = _frames.Describe();

            if (!_sharing && Volatile.Read(ref _wanted).IsOn)
            {
                generation = $"{GpuPictureReport.GenerationOff}: the pictures are read back (it runs on the GPU path only)";
            }

            return new GpuPictureReport(Volatile.Read(ref _path), generation, DescribeUpscale());
        }
    }

    public void Offer(long adapterLuid)
    {
        Interlocked.CompareExchange(ref _offered, adapterLuid, 0);
    }

    public void Use(FrameGeneration settings)
    {
        Volatile.Write(ref _wanted, settings);
    }

    public void Scale(PictureScale scale)
    {
        Volatile.Write(ref _wantedScale, scale);
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
    public bool Publish(PictureKind kind, long decodedAt, uint rtpTimestamp, out int width, out int height)
    {
        width = _decoder.PictureWidth;
        height = _decoder.PictureHeight;

        if (!_decoder.TryTakeSurface(out var surface, out var slice) || !Prepare(surface, width, height))
        {
            return false;
        }

        // A repeat adds nothing the generated frames do not already show.
        if (_frames.IsOn && kind == PictureKind.Repeat)
        {
            return false;
        }

        BeginUpscale();
        bool published;

        if (_frames.IsOn)
        {
            published = PublishGenerated(surface, slice, decodedAt, rtpTimestamp, width, height);
        }
        else
        {
            published = PublishDecoded(surface, slice, decodedAt, width, height);
        }

        EndUpscale();

        return published;
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
        _frames.Dispose();
        _upscale.Dispose();
        _decoder.Dispose();
        _ring?.Dispose();

        foreach (var ring in _retired)
        {
            ring.Dispose();
        }

        _retired.Clear();
    }

    // The decoded picture alone: copied into a slot, the fence set, published.
    [HotPath]
    private bool PublishDecoded(nint surface, uint slice, long decodedAt, int width, int height)
    {
        if (!TryClaim(out var slot))
        {
            return false;
        }

        WriteDecoded(slot, surface, slice);
        Add(slot, decodedAt, width, height, generated: false);
        _slots.Publish();

        return true;
    }

    // The frames generated between the last new picture and this one, each in a slot, then this one; published as one group.
    [HotPath]
    private bool PublishGenerated(nint surface, uint slice, long decodedAt, uint rtpTimestamp, int width, int height)
    {
        var count = _frames.Generate(surface, slice, rtpTimestamp);

        for (var index = 0; index < count; index++)
        {
            if (!TryClaim(out var slot))
            {
                break;
            }

            WriteGenerated(index, slot);
            Add(slot, decodedAt, width, height, generated: true);
        }

        _frames.Done();

        return PublishDecoded(surface, slice, decodedAt, width, height);
    }

    // The decoded picture into a slot: copied as it is, or upscaled (from the frame generation's RGBA of it when that runs).
    [HotPath]
    private void WriteDecoded(int slot, nint surface, uint slice)
    {
        var target = _ring!.Texture(slot);

        if (!_upscale.IsOn)
        {
            _ring.Copy(slot, surface, slice);
        }
        else if (_frames.IsOn)
        {
            _ = _upscale.WriteRgba(_frames.Picture, target);
        }
        else
        {
            _ = _upscale.Write(surface, slice, target);
        }
    }

    // A generated frame into a slot: converted to NV12 as it is, or upscaled.
    [HotPath]
    private void WriteGenerated(int index, int slot)
    {
        var target = _ring!.Texture(slot);

        if (_upscale.IsOn)
        {
            _ = _upscale.WriteRgba(_frames.Generated(index), target);

            return;
        }

        _ = _frames.Write(index, target);
    }

    [HotPath]
    private void BeginUpscale()
    {
        if (_upscale.IsOn)
        {
            _upscale.Begin();
        }
    }

    [HotPath]
    private void EndUpscale()
    {
        if (_upscale.IsOn)
        {
            _upscale.Done();
        }
    }

    // A free slot, once the decoder's queue waits on the GPU for the renderer's last read of it.
    [HotPath]
    private bool TryClaim(out int slot)
    {
        if (!_slots.TryClaim(out slot, out var release))
        {
            return false;
        }

        if (_ring!.WaitForReader(release.Fence, release.Value))
        {
            return true;
        }

        _slots.Abandon(slot);
        StopSharing("the decoder's device could not open the renderer's fence");

        return false;
    }

    // The slot is written: the fence is set once the GPU is done, and the picture joins the group.
    [HotPath]
    private void Add(int slot, long decodedAt, int width, int height, bool generated)
    {
        var ready = _ring!.Signal();

        // An upscaled picture is shown at the window's size (its slot may be a texel larger: NV12's even sizes).
        if (_upscale.IsOn)
        {
            (width, height) = (_upscale.Width, _upscale.Height);
        }

        _slots.Add(new GpuPicture(_generation, slot, ready, width, height, _decoder.PictureColour, decodedAt, generated));
    }

    // The upscaler, the ring and the frame generation as this picture needs them; false when the ring cannot be made.
    private bool Prepare(nint surface, int width, int height)
    {
        ApplyUpscale(width, height);

        if (!EnsureRing(surface))
        {
            return false;
        }

        if (_upscale.IsOn && !_ring!.IsWritableByVideoProcessor)
        {
            _upscale.Refuse("the GPU cannot write the shared NV12 textures with its video processor");

            if (!EnsureRing(surface))
            {
                return false;
            }
        }

        ApplyGeneration(width, height);

        return true;
    }

    // The renderer's upscaling request, set up again when it or the picture's size changed.
    private void ApplyUpscale(int width, int height)
    {
        var wanted = Volatile.Read(ref _wantedScale);

        if (_upscale.Differs(wanted, width, height))
        {
            _upscale.Apply(wanted, _decoder.Device, width, height);
        }
    }

    // What the upscaler does, or why it cannot: it needs the GPU path.
    private string DescribeUpscale()
    {
        var wanted = Volatile.Read(ref _wantedScale);

        if (!_sharing && wanted.IsOn)
        {
            return $"{wanted.Method.Label()} off: the pictures are read back (it runs on the GPU path only)";
        }

        return _upscale.Describe();
    }

    // The profile's frame generation, set up again when it or the picture's size changed.
    private void ApplyGeneration(int width, int height)
    {
        var wanted = Volatile.Read(ref _wanted);

        if (ReferenceEquals(wanted, _applied) && width == _frames.Width && height == _frames.Height)
        {
            return;
        }

        _applied = wanted;
        _slots.UsePacer(_frames.Apply(wanted, _decoder.Device, _ring!.IsWritableByVideoProcessor, width, height));
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
        Volatile.Write(ref _path, GpuPictureReport.ZeroCopy);
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

    // A ring the size of the decoder's surfaces, or of the upscaled picture's slots while upscaling: made at the first
    // picture, and again when that size changes.
    private bool EnsureRing(nint surface)
    {
        var upscaled = _upscale.IsOn;

        if (_ring is not null && upscaled == _ringUpscaled && Fits(surface))
        {
            return true;
        }

        var ring = upscaled
            ? SharedSurfaceRing.TryCreate(_decoder.Device, RingSlots, _upscale.SlotWidth, _upscale.SlotHeight, out var problem)
            : SharedSurfaceRing.TryCreateFor(_decoder.Device, RingSlots, surface, out problem);

        if (ring is null)
        {
            StopSharing(problem ?? "the shared textures could not be made");

            return false;
        }

        Retire();
        (_ring, _ringUpscaled) = (ring, upscaled);
        Share(ring);

        return true;
    }

    private bool Fits(nint surface)
    {
        if (_ringUpscaled)
        {
            return _ring!.Width == _upscale.SlotWidth && _ring.Height == _upscale.SlotHeight;
        }

        return _ring!.Fits(surface);
    }

    // The ring being replaced is kept a while; the oldest kept goes.
    private void Retire()
    {
        if (_ring is null)
        {
            return;
        }

        _retired.Add(_ring);

        if (_retired.Count > MaxRetired)
        {
            _retired[0].Dispose();
            _retired.RemoveAt(0);
        }
    }

    private void Share(SharedSurfaceRing ring)
    {
        _generation++;
        _slots.Reset(_generation);
        _applied = null;

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
        _frames.Dispose();
        _upscale.Dispose();
        Volatile.Write(ref _path, $"{GpuPictureReport.ReadBack}: {reason}");
        _log.ReadBack(reason);
    }
}
