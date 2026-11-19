// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices.Marshalling;
using Microsoft.Extensions.Logging;
using Phantom.Relay.Lsfg.Logging;

namespace Phantom.Relay.Lsfg.Shaders;

/// <summary>
/// Every compute shader in the user's Lossless.dll, by resource id, with what D3DReflect reports about it. The
/// library stays mapped while the catalog lives, so the pipeline can create its shaders from the mapped bytes.
/// </summary>
public sealed class LsfgShaderCatalog : IDisposable
{
    private readonly LosslessLibrary _library;
    private readonly Dictionary<int, ShaderLayout> _shaders;

    private LsfgShaderCatalog(LosslessLibrary library, Dictionary<int, ShaderLayout> shaders)
    {
        _library = library;
        _shaders = shaders;
    }

    /// <summary>The raw-data resources the file holds, shaders or not.</summary>
    public int ResourceCount => _library.ResourceIds.Count;

    /// <summary>The compute shaders, by resource id.</summary>
    public IReadOnlyDictionary<int, ShaderLayout> Shaders => _shaders;

    /// <summary>Maps the file and reflects every compute shader in it; null and why when the file cannot be read.</summary>
    public static LsfgShaderCatalog? TryLoad(string path, ILogger log, out string? problem)
    {
        var library = LosslessLibrary.TryOpen(path, out problem);

        if (library is null)
        {
            return null;
        }

        var wrappers = new StrategyBasedComWrappers();
        var shaders = new Dictionary<int, ShaderLayout>();

        foreach (var id in library.ResourceIds)
        {
            var blob = library.Read(id);

            if (!ShaderReflector.IsShaderContainer(blob))
            {
                log.ResourceSkipped(id, "not a DXBC container");

                continue;
            }

            var layout = ShaderReflector.TryReflect(wrappers, id, blob, out var reason);

            if (layout is null)
            {
                log.ResourceSkipped(id, reason ?? "not reflected");

                continue;
            }

            shaders[id] = layout;
            log.ShaderReflected(id, layout.GroupWidth, layout.GroupHeight, layout.Textures, layout.UnorderedAccesses, layout.Samplers, layout.ConstantBuffers, layout.ConstantBufferSize);
        }

        log.CatalogLoaded(library.ResourceIds.Count, shaders.Count);

        return new LsfgShaderCatalog(library, shaders);
    }

    /// <summary>A shader's compiled bytes, read from the mapped file; valid until the catalog is disposed.</summary>
    public ReadOnlySpan<byte> Bytecode(int id)
    {
        return _library.Read(id);
    }

    public void Dispose()
    {
        _library.Dispose();
    }
}
