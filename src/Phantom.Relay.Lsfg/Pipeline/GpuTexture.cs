// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>A 2D texture the passes read (its shader view) and, when writable, write (its unordered-access view).</summary>
internal sealed class GpuTexture : IDisposable
{
    private nint _texture;
    private nint _view;
    private nint _output;

    private GpuTexture(nint texture, nint view, nint output, int width, int height, uint format)
    {
        _texture = texture;
        _view = view;
        _output = output;
        Width = width;
        Height = height;
        Format = format;
    }

    public nint Texture => _texture;

    /// <summary>The shader resource view a pass reads it through.</summary>
    public nint View => _view;

    /// <summary>The unordered-access view a pass writes it through, or 0 when it is read-only.</summary>
    public nint Output => _output;

    public int Width { get; }

    public int Height { get; }

    public uint Format { get; }

    /// <summary>Views over a texture this object then owns; null (and the texture released) when a view is refused.</summary>
    public static GpuTexture? Wrap(ComputeDevice device, nint texture, int width, int height, uint format, bool writable)
    {
        var read = new ShaderResourceViewDescription
        {
            Format = format,
            ViewDimension = Direct3D11Values.ShaderResourceTexture2D,
            MipLevels = 1,
        };

        _ = device.DeviceApi.CreateShaderResourceView(texture, in read, out var view);
        var output = (nint)0;

        if (writable)
        {
            var write = new UnorderedAccessViewDescription { Format = format, ViewDimension = Direct3D11Values.UnorderedAccessTexture2D };
            _ = device.DeviceApi.CreateUnorderedAccessView(texture, in write, out output);
        }

        if (view == 0 || (writable && output == 0))
        {
            ComObjects.Release(ref output);
            ComObjects.Release(ref view);
            ComObjects.Release(ref texture);

            return null;
        }

        return new GpuTexture(texture, view, output, width, height, format);
    }

    public void Dispose()
    {
        ComObjects.Release(ref _output);
        ComObjects.Release(ref _view);
        ComObjects.Release(ref _texture);
    }
}
