// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Media.Windows.Interop;
using Phantom.Relay.Nvidia.Interop;
using Phantom.Relay.Nvidia.Logging;

namespace Phantom.Relay.Nvidia;

/// <summary>
/// NVIDIA's FRUC on a D3D11 device (the hardware decoder's): the frames between each new frame and the one before,
/// made on the GPU's optical flow accelerator and CUDA. FRUC reads and writes only textures registered with it, so the
/// generator makes its own: two inputs (used in turn: FRUC keeps the previous frame by reference) and three outputs
/// (x4's frames), shareable (NT handles, for CUDA's D3D11 interop), registered once and unregistered on
/// <see cref="Dispose"/>. Each new frame is copied into the next input (<see cref="Submit"/>); FRUC makes the frame at
/// the first time asked for, and the later ones of the same pair from the same frame passed again with later times.
/// Both sides synchronise on one shared ID3D11Fence, never on the CPU: the device's queue sets it once the copy is
/// done and FRUC waits for that value before reading; FRUC sets the next value once an output is written; the
/// device's queue waits for the last before anything later (a read of an output, the next copy).
/// </summary>
/// <remarks>
/// Measured on the SDK's 5.0.7 library (docs/architecture/nvidia-fruc.md): NvOFFRUCProcess holds the calling thread
/// until its work is done (about 4 ms a frame at 1440p on an RTX 4090); one FRUC per process at a time (a second one
/// alive beside the first breaks the first's synchronisation or crashes the process), and one per D3D11 device for
/// the device's life (after one is destroyed the next on the same device fails to share its fence; a fresh device
/// works). So a caller keeps one generator per decoder device, pauses it rather than destroying it
/// (<see cref="Restart"/>), and disposes it with the device. FRUC writes its own errors to
/// %ProgramData%\NVIDIA Corporation\NvOFFRUC\logFRUCError.txt, a line for each later frame of a pair too (it calls the
/// same frame passed again out of order, yet makes the frame).
/// </remarks>
public sealed unsafe class FrucGenerator : IDisposable
{
    /// <summary>The most frames made per pair: x4.</summary>
    public const int MaxGenerated = 3;

    /// <summary>The fewest frames shown per new frame: x2, one made.</summary>
    private const int MinimumMultiplier = 2;

    /// <summary>The inputs used in turn.</summary>
    private const int Inputs = 2;

    /// <summary>Inputs and outputs: at least NvOFFRUC_MIN_RESOURCE, at most NvOFFRUC_MAX_RESOURCE.</summary>
    private const int Textures = Inputs + MaxGenerated;

    /// <summary>FRUC's time stamp of the first frame, as the SDK's sample counts them: one per frame from 1.</summary>
    private const double FirstTimeStamp = 1;

    private readonly FrucLibrary _library;
    private readonly ComputeDevice _device;
    private readonly nint[] _inputs = new nint[Inputs];
    private readonly nint[] _outputs = new nint[MaxGenerated];
    private nint _context4;
    private nint _fence;
    private nint _handle;
    private byte* _repeated;
    private ulong _fenceValue;
    private int _next;
    private long _since;
    private bool _registered;

    private FrucGenerator(FrucLibrary library, ComputeDevice device, int width, int height, FrucSurfaceFormat format)
    {
        _library = library;
        _device = device;
        Width = width;
        Height = height;
        Format = format;
        _repeated = (byte*)NativeMemory.AllocZeroed(1);
    }

    public int Width { get; }

    public int Height { get; }

    public FrucSurfaceFormat Format { get; }

    /// <summary>The frames submitted so far.</summary>
    public long Submitted { get; private set; }

    /// <summary>How often FRUC judged a frame too poor and gave the previous frame back instead.</summary>
    public long Repeats { get; private set; }

    /// <summary>Why the last <see cref="Submit"/> failed (FRUC's status), or null.</summary>
    public string? Failure { get; private set; }

    internal ComputeDevice Device => _device;

    /// <summary>
    /// FRUC from the library in <paramref name="folder"/> (see <see cref="NvidiaFrucLocator"/>) for frames of
    /// <paramref name="width"/> x <paramref name="height"/> in <paramref name="format"/> on <paramref name="device"/> (an
    /// ID3D11Device the caller keeps; the generator holds its own reference); null and why when the GPU is not an
    /// NVIDIA one, the library will not load, or FRUC or the device refuses a step.
    /// </summary>
    public static FrucGenerator? TryCreate(string? folder, nint device, int width, int height, FrucSurfaceFormat format, ILogger log, out string? problem)
    {
        problem = Refusal(folder, device);
        FrucLibrary? library = null;

        if (problem is null)
        {
            library = FrucLibrary.TryLoad(folder!, out problem);
        }

        if (library is null)
        {
            log.FrucUnavailable(problem!);

            return null;
        }

        var generator = new FrucGenerator(library, ComputeDevice.Open(new StrategyBasedComWrappers(), device), width, height, format);
        problem = generator.Prepare();

        if (problem is not null)
        {
            generator.Dispose();
            log.FrucUnavailable(problem);

            return null;
        }

        log.FrucReady(width, height, format, Textures);

        return generator;
    }

    /// <summary>
    /// Takes a new frame (subresource <paramref name="subresource"/> of <paramref name="source"/>, a texture of
    /// <see cref="Format"/> at least the generator's size: a decoder's surface may be padded) and queues the
    /// <paramref name="multiplier"/> - 1 frames between the one before and it (x2 to x4) into <see cref="Output"/> 0, 1
    /// and 2, in display order; how many (none for the first frame since <see cref="Restart"/>, or when FRUC refused
    /// the first: <see cref="Failure"/>; fewer when it refused a later one).
    /// </summary>
    [HotPath]
    public int Submit(nint source, uint subresource, int multiplier)
    {
        var wanted = Math.Clamp(multiplier, MinimumMultiplier, MaxGenerated + 1);
        var input = _inputs[_next];
        _next = (_next + 1) % Inputs;
        Copy(input, source, subresource);

        _fenceValue++;
        _ = SharingCalls.Signal(_context4, _fence, _fenceValue);
        SharingCalls.Flush(_device.Context);

        var made = Interpolate(input, wanted);

        if (made > 0)
        {
            _ = SharingCalls.Wait(_context4, _fence, _fenceValue);
            Submitted++;
            _since++;
        }

        if (_since > 1)
        {
            return made;
        }

        return 0;
    }

    /// <summary>Frame <paramref name="index"/> of the last <see cref="Submit"/> (a texture of <see cref="Format"/> the generator owns), valid until the next.</summary>
    public nint Output(int index)
    {
        return _outputs[index];
    }

    /// <summary>The frames stop for a while (frame generation off): the next one is a first again, its frames unused.</summary>
    public void Restart()
    {
        _since = 0;
        Failure = null;
    }

    public void Dispose()
    {
        Unregister();

        if (_handle != 0)
        {
            _ = _library.Destroy(_handle);
            _handle = 0;
        }

        Release(_inputs);
        Release(_outputs);
        ComObjects.Release(ref _fence);
        ComObjects.Release(ref _context4);
        NativeMemory.Free(_repeated);
        _repeated = null;

        _library.Dispose();
        _device.Dispose();
    }

    // Why FRUC cannot run on this device or from this folder, before anything is loaded; null when it may.
    private static string? Refusal(string? folder, nint device)
    {
        if (NvidiaAdapters.VendorOf(device) != NvidiaAdapters.NvidiaVendor)
        {
            return "the decoder's GPU is not an NVIDIA one (FRUC runs on NVIDIA's optical flow accelerator)";
        }

        if (string.IsNullOrEmpty(folder))
        {
            return "NVIDIA's Optical Flow SDK (NvOFFRUC.dll) was not found";
        }

        return null;
    }

    private static void Release(nint[] textures)
    {
        for (var index = 0; index < textures.Length; index++)
        {
            ComObjects.Release(ref textures[index]);
        }
    }

    // The new frame into an input texture: the generator's size from the top-left corner.
    [HotPath]
    private void Copy(nint input, nint source, uint subresource)
    {
        CopyBox box = default;
        box.Right = (uint)Width;
        box.Bottom = (uint)Height;
        box.Back = 1;
        _device.ContextApi.CopySubresourceRegion(input, 0, 0, 0, 0, source, subresource, (nint)(&box));
    }

    // The pair's frames in display order, each a call (the same frame passed again for the later ones); only the first
    // for a first frame, whose frames are not used. How many FRUC made.
    [HotPath]
    private int Interpolate(nint input, int multiplier)
    {
        var count = multiplier - 1;

        if (_since == 0)
        {
            count = 1;
        }

        for (var index = 0; index < count; index++)
        {
            var back = 1 - ((index + 1) / (double)multiplier);
            var status = Process(input, _outputs[index], back);

            if (status != FrucValues.Success)
            {
                Failure = FrucStatus.Describe(status);

                return index;
            }
        }

        return count;
    }

    // NvOFFRUCProcess: wait for the fence's value, write the output at <back> of a frame before the newest, set the next value.
    [HotPath]
    private int Process(nint input, nint output, double back)
    {
        var now = FirstTimeStamp + Submitted;

        FrucProcessInput parameters = default;
        parameters.Frame.Frame = input;
        parameters.Frame.TimeStamp = now;
        parameters.WaitValue = _fenceValue;

        FrucProcessOutput result = default;
        result.Frame.Frame = output;
        result.Frame.TimeStamp = now - back;
        result.Frame.RepetitionOccurred = _repeated;
        result.SignalValue = _fenceValue + 1;

        var status = _library.Process(_handle, &parameters, &result);

        if (status == FrucValues.Success)
        {
            _fenceValue++;
            CountRepeat();
        }

        return status;
    }

    [HotPath]
    private void CountRepeat()
    {
        if (*_repeated == 0)
        {
            return;
        }

        Repeats++;
        *_repeated = 0;
    }

    // The fence, the instance, the textures, and their registration.
    private string? Prepare()
    {
        var problem = PrepareFence();

        if (problem is not null)
        {
            return problem;
        }

        problem = CreateInstance();

        if (problem is not null)
        {
            return problem;
        }

        problem = CreateTextures();

        if (problem is not null)
        {
            return problem;
        }

        return Register();
    }

    private string? PrepareFence()
    {
        var device5 = SharingCalls.Query(_device.Device, in SharingCalls.Device5);
        _context4 = SharingCalls.Query(_device.Context, in SharingCalls.DeviceContext4);

        if (device5 == 0 || _context4 == 0)
        {
            ComObjects.Release(ref device5);

            return "the D3D11 device has no fences (ID3D11Device5, ID3D11DeviceContext4: Windows 10 1703 or later)";
        }

        var result = SharingCalls.CreateFence(device5, out _fence);
        ComObjects.Release(ref device5);

        if (ComObjects.Failed(result))
        {
            return $"the D3D11 device refused a shared fence (0x{result:X8})";
        }

        return null;
    }

    private string? CreateInstance()
    {
        FrucCreateParameters parameters = default;
        parameters.Width = (uint)Width;
        parameters.Height = (uint)Height;
        parameters.Device = _device.Device;

        parameters.ResourceType = FrucValues.DirectX11Resource;
        parameters.SurfaceFormat = FrucValues.ArgbSurface;
        parameters.CudaResourceType = FrucValues.CudaResourceUndefined;

        if (Format == FrucSurfaceFormat.Nv12)
        {
            parameters.SurfaceFormat = FrucValues.Nv12Surface;
        }

        nint handle = 0;
        var status = _library.Create(&parameters, &handle);

        if (status != FrucValues.Success)
        {
            return $"NvOFFRUCCreate refused {Width}x{Height}: {FrucStatus.Describe(status)}";
        }

        _handle = handle;

        return null;
    }

    // Shareable textures (FRUC opens them in CUDA by their NT handles); the outputs are also read by a video processor.
    private string? CreateTextures()
    {
        var description = new Texture2DDescription
        {
            Width = (uint)Width,
            Height = (uint)Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Direct3D11Values.Rgba8Format,
            SampleCount = 1,
            Usage = Direct3D11Values.DefaultUsage,
            BindFlags = Direct3D11Values.BindShaderResource | Direct3D11Values.BindRenderTarget,
            MiscFlags = Direct3D11Values.MiscShared | Direct3D11Values.MiscSharedNtHandle,
        };

        if (Format == FrucSurfaceFormat.Nv12)
        {
            description.Format = MediaFoundationValues.Nv12Format;
        }

        var result = 0;

        for (var index = 0; index < Textures && !ComObjects.Failed(result); index++)
        {
            result = _device.DeviceApi.CreateTexture2D(in description, 0, out Slot(index));
        }

        if (ComObjects.Failed(result))
        {
            return $"the D3D11 device refused a shared {Format} texture of {Width}x{Height} (0x{result:X8})";
        }

        return null;
    }

    // The textures in registration order: the outputs first, then the inputs, as the SDK's sample lists them.
    private ref nint Slot(int index)
    {
        if (index < MaxGenerated)
        {
            return ref _outputs[index];
        }

        return ref _inputs[index - MaxGenerated];
    }

    private string? Register()
    {
        FrucRegistration registration = default;
        registration.Fence = _fence;
        registration.Count = Textures;

        for (var index = 0; index < Textures; index++)
        {
            registration.Resources[index] = Slot(index);
        }

        var status = _library.Register(_handle, &registration);

        if (status != FrucValues.Success)
        {
            return $"NvOFFRUCRegisterResource refused the textures: {FrucStatus.Describe(status)} (as it does for a second FRUC on one device)";
        }

        _registered = true;

        return null;
    }

    private void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        FrucUnregistration registration = default;
        registration.Count = Textures;

        for (var index = 0; index < Textures; index++)
        {
            registration.Resources[index] = Slot(index);
        }

        _ = _library.Unregister(_handle, &registration);
        _registered = false;
    }
}
