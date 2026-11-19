// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// Who holds which slot of a ring of GPU pictures, between the decode thread that writes them and the render thread
/// that shows them. The decode thread claims a free slot (<see cref="TryClaim"/>, with what the renderer's last read
/// of it ends at, to wait for on the GPU), writes it, adds it to the group it builds (<see cref="Add"/>) and publishes
/// the group (<see cref="Publish"/>): one decoded picture, or with frame generation the frames before it and then it.
/// At each refresh the render thread asks the pacer which picture of the newest group to show
/// (<see cref="TryTake"/>); the picture it replaces goes back to free, and after each frame's read the renderer says
/// what that read ends at (<see cref="Release"/>). A new group frees what was pending of the one before, so the newest
/// picture always wins. One short lock guards it all; nothing is allocated after construction.
/// </summary>
/// <remarks>
/// With groups of at most G pictures, 2G + 1 slots are always enough: the group being written, the pending group and
/// the picture on screen. Without frame generation (G = 1) that is a triple buffer.
/// </remarks>
public sealed class GpuPictureSlots
{
    /// <summary>A pacer's answer for "keep what is on screen".</summary>
    public const int Hold = -1;

    private const int NoSlot = -1;

    // Two groups and the picture on screen.
    private const int GroupsHeld = 2;

    private readonly Lock _gate = new();
    private readonly GpuSlotState[] _states;
    private readonly GpuPicture[] _pictures;
    private readonly GpuSlotRelease[] _releases;
    private readonly int[] _pending;
    private readonly int[] _building;
    private IPicturePacer _pacer;
    private int _pendingCount;
    private int _buildingCount;
    private long _groups;
    private int _shown = NoSlot;
    private int _cursor;
    private int _generation;

    /// <summary>A queue of <paramref name="slots"/> slots for groups of at most <paramref name="maxGroup"/> pictures, paced by <paramref name="pacer"/>.</summary>
    public GpuPictureSlots(int slots, int maxGroup, IPicturePacer pacer)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxGroup);
        ArgumentOutOfRangeException.ThrowIfLessThan(slots, (GroupsHeld * maxGroup) + 1);
        ArgumentNullException.ThrowIfNull(pacer);

        _states = new GpuSlotState[slots];
        _pictures = new GpuPicture[slots];
        _releases = new GpuSlotRelease[slots];
        _pending = new int[maxGroup];
        _building = new int[maxGroup];
        _pacer = pacer;
    }

    public int Slots => _states.Length;

    /// <summary>The most pictures one group holds.</summary>
    public int MaxGroup => _pending.Length;

    /// <summary>The groups published so far.</summary>
    public long Groups
    {
        get
        {
            lock (_gate)
            {
                return _groups;
            }
        }
    }

    /// <summary>Where slot <paramref name="slot"/> is now.</summary>
    public GpuSlotState StateOf(int slot)
    {
        lock (_gate)
        {
            return _states[slot];
        }
    }

    /// <summary>
    /// The decode thread starts over on ring <paramref name="generation"/>: every slot free, nothing pending or shown,
    /// and no read to wait for (the old ring's reads were of other textures). A release of the old ring is ignored.
    /// </summary>
    public void Reset(int generation)
    {
        lock (_gate)
        {
            Array.Fill(_states, GpuSlotState.Free);
            Array.Clear(_releases);
            _pendingCount = 0;
            _buildingCount = 0;
            _shown = NoSlot;
            _generation = generation;
        }
    }

    /// <summary>Paces the groups from the next one on with <paramref name="pacer"/>.</summary>
    public void UsePacer(IPicturePacer pacer)
    {
        ArgumentNullException.ThrowIfNull(pacer);

        lock (_gate)
        {
            _pacer = pacer;
        }
    }

    /// <summary>
    /// The decode thread claims a free slot to write, the one freed longest ago first, with what the renderer's last
    /// read of it ends at; false when none is free (more pictures than the queue was made for).
    /// </summary>
    [HotPath]
    public bool TryClaim(out int slot, out GpuSlotRelease release)
    {
        lock (_gate)
        {
            for (var step = 0; step < _states.Length; step++)
            {
                var candidate = (_cursor + step) % _states.Length;

                if (_states[candidate] == GpuSlotState.Free)
                {
                    _states[candidate] = GpuSlotState.Writing;
                    _cursor = candidate + 1;
                    slot = candidate;
                    release = _releases[candidate];

                    return true;
                }
            }
        }

        slot = NoSlot;
        release = default;

        return false;
    }

    /// <summary>A slot the decode thread claimed holds <paramref name="picture"/>: the next of the group it builds.</summary>
    [HotPath]
    public void Add(in GpuPicture picture)
    {
        lock (_gate)
        {
            if (_states[picture.Slot] != GpuSlotState.Writing || _buildingCount == _building.Length)
            {
                throw new InvalidOperationException("a picture goes into a slot claimed for it, at most MaxGroup a group");
            }

            _pictures[picture.Slot] = picture;
            _building[_buildingCount] = picture.Slot;
            _buildingCount++;
        }
    }

    /// <summary>A claimed slot the decode thread did not write after all goes back to free.</summary>
    [HotPath]
    public void Abandon(int slot)
    {
        lock (_gate)
        {
            if (_states[slot] == GpuSlotState.Writing)
            {
                _states[slot] = GpuSlotState.Free;
            }
        }
    }

    /// <summary>The group built so far becomes the newest: what was pending of the one before is freed, and the pacer is told.</summary>
    [HotPath]
    public void Publish()
    {
        lock (_gate)
        {
            if (_buildingCount == 0)
            {
                return;
            }

            FreePending();
            Array.Copy(_building, _pending, _buildingCount);
            _pendingCount = _buildingCount;
            _buildingCount = 0;

            for (var index = 0; index < _pendingCount; index++)
            {
                _states[_pending[index]] = GpuSlotState.Pending;
            }

            _groups++;
            _pacer.OnGroup(_groups, _pendingCount);
        }
    }

    /// <summary>
    /// The render thread, once a refresh: the picture the pacer puts on screen now, which replaces the one shown (that
    /// slot goes back to free); false to keep showing what is on screen.
    /// </summary>
    [HotPath]
    public bool TryTake(out GpuPicture picture)
    {
        lock (_gate)
        {
            var index = _pacer.OnRefresh();

            if (index >= 0 && index < _pendingCount && _states[_pending[index]] == GpuSlotState.Pending)
            {
                Show(_pending[index]);
                picture = _pictures[_shown];

                return true;
            }
        }

        picture = default;

        return false;
    }

    /// <summary>The render thread's read of <paramref name="picture"/> this frame ends at <paramref name="release"/>.</summary>
    [HotPath]
    public void Release(in GpuPicture picture, GpuSlotRelease release)
    {
        lock (_gate)
        {
            if (picture.Generation == _generation)
            {
                _releases[picture.Slot] = release;
            }
        }
    }

    private void Show(int slot)
    {
        if (_shown != NoSlot)
        {
            _states[_shown] = GpuSlotState.Free;
        }

        _states[slot] = GpuSlotState.Shown;
        _shown = slot;
    }

    private void FreePending()
    {
        for (var index = 0; index < _pendingCount; index++)
        {
            var slot = _pending[index];

            if (_states[slot] == GpuSlotState.Pending)
            {
                _states[slot] = GpuSlotState.Free;
            }
        }
    }
}
