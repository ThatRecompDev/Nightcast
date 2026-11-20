// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.App.Pictures;

/// <summary>
/// The frame generation the running stream asks for, as the profile sets it and the player changes it live: F4 cycles
/// through off, Lossless Scaling's frame generation and NVIDIA's FRUC (<see cref="Cycle"/>), F5 steps through a small
/// set of LSFG's settings (<see cref="Next"/>): the quality shaders, the performance ones, each with motion estimated on
/// the whole picture then on half of it. Each keeps the profile's multiplier. A new profile (another title) starts from
/// its own setting again (<see cref="Follow"/>). Render thread only.
/// </summary>
public sealed class FrameGenerationSwitch
{
    /// <summary>Motion estimated at half the picture's size: about half the flow's cost.</summary>
    public const float HalfFlow = 0.5f;

    private const string OffLabel = "off";
    private const string QualityName = "quality";
    private const string PerformanceName = "performance";

    private static readonly FrameGeneration PerformanceDoubled = FrameGeneration.QualityDoubled with { Mode = FrameGenerationMode.Performance };

    /// <summary>F5's settings, in order.</summary>
    private static readonly FrameGeneration[] Settings =
    [
        FrameGeneration.QualityDoubled,
        PerformanceDoubled,
        FrameGeneration.QualityDoubled with { FlowScale = HalfFlow },
        PerformanceDoubled with { FlowScale = HalfFlow },
    ];

    private FrameGeneration _chosen = FrameGeneration.QualityDoubled;
    private bool _on;

    /// <summary>What the stream asks for now: the chosen setting, or <see cref="FrameGeneration.Off"/>.</summary>
    public FrameGeneration Current
    {
        get
        {
            if (_on)
            {
                return _chosen;
            }

            return FrameGeneration.Off;
        }
    }

    /// <summary>Whether the player changed it during this profile.</summary>
    public bool Changed { get; private set; }

    /// <summary>A profile's setting plays: on as it says, or off with the first of F5's settings ready for F4.</summary>
    public FrameGeneration Follow(FrameGeneration profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _on = profile.IsOn;
        Changed = false;
        _chosen = FrameGeneration.QualityDoubled;

        if (profile.IsOn)
        {
            _chosen = profile;
        }

        return Current;
    }

    /// <summary>F4: off → LSFG → NVIDIA FRUC → off, with the setting chosen last.</summary>
    public FrameGeneration Cycle()
    {
        Changed = true;

        if (!_on)
        {
            _on = true;
            _chosen = _chosen with { Generator = FrameGenerator.Lsfg };
        }
        else if (_chosen.Generator == FrameGenerator.Lsfg)
        {
            _chosen = _chosen with { Generator = FrameGenerator.NvidiaFruc };
        }
        else
        {
            _on = false;
            _chosen = _chosen with { Generator = FrameGenerator.Lsfg };
        }

        return Current;
    }

    /// <summary>F5: the next of LSFG's settings (the first when the current is none of them), turned on with LSFG.</summary>
    public FrameGeneration Next()
    {
        var next = Settings[(IndexOf(_chosen) + 1) % Settings.Length];
        _chosen = next with { Multiplier = _chosen.ClampedMultiplier };
        _on = true;
        Changed = true;

        return Current;
    }

    /// <summary>"LSFG x2 quality, flow 1.00", "NVIDIA FRUC x2", or "off".</summary>
    public string Describe()
    {
        if (!_on)
        {
            return OffLabel;
        }

        if (_chosen.Generator == FrameGenerator.NvidiaFruc)
        {
            return string.Create(CultureInfo.InvariantCulture, $"NVIDIA FRUC x{_chosen.ClampedMultiplier}");
        }

        var mode = QualityName;

        if (_chosen.Mode == FrameGenerationMode.Performance)
        {
            mode = PerformanceName;
        }

        return string.Create(CultureInfo.InvariantCulture, $"LSFG x{_chosen.ClampedMultiplier} {mode}, flow {_chosen.ClampedFlowScale:F2}");
    }

    // Which of the settings this is by its shaders and flow (the multiplier aside); -1 for none.
    private static int IndexOf(FrameGeneration setting)
    {
        for (var index = 0; index < Settings.Length; index++)
        {
            if (Settings[index].Mode == setting.Mode && Settings[index].ClampedFlowScale.Equals(setting.ClampedFlowScale))
            {
                return index;
            }
        }

        return -1;
    }
}
