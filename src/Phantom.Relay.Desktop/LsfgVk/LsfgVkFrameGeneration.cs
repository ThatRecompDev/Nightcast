// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Pictures;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// Frame generation on Linux through lsfg-vk, without Decky and without launch options. A Vulkan layer is read when a
/// Vulkan instance is made, so the layer is loaded before the window opens, when the player's kept choice is on
/// (<see cref="StartAsync"/>): the pinned build made ready in Nightcast's own folder (<see cref="LsfgVkInstaller"/>),
/// Nightcast's configuration written (<see cref="LsfgVkConfig"/>), the layer's variables set in this process
/// (<see cref="LsfgVkEnvironment"/>) and the loader asked whether the layer loaded (<see cref="ILayerProbe"/>). Once
/// loaded, every choice of the menu or F4, F5 and F8 is written to the configuration, which the layer takes live (off
/// is multiplier 1). Turned on while not loaded, it is made ready in the background and then asks for the process to
/// start again (<see cref="RestartRequested"/>); the head does that once the window closed. Without the player's
/// Lossless Scaling (its lsfg-vk branch) nothing is downloaded. Every choice is kept for the next start.
/// </summary>
public sealed class LsfgVkFrameGeneration : IExternalFrameGeneration
{
    private const string Name = "lsfg-vk";

    private readonly LsfgVkFolders _folders;
    private readonly LsfgVkLossless _lossless;
    private readonly LsfgVkInstaller _installer;
    private readonly IProcessEnvironment _environment;
    private readonly ILayerProbe _probe;
    private readonly LsfgVkChoiceStore _choices;
    private readonly Lock _gate = new();

    private LsfgVkState _state = LsfgVkState.Off;
    private string? _problem;
    private FrameGeneration _chosen;

    public LsfgVkFrameGeneration(LsfgVkFolders folders, LsfgVkLossless lossless, LsfgVkInstaller installer, IProcessEnvironment environment, ILayerProbe probe, LsfgVkChoiceStore choices)
    {
        _folders = folders;
        _lossless = lossless;
        _installer = installer;
        _environment = environment;
        _probe = probe;
        _choices = choices;

        _chosen = choices.Load();
        Remembered = _chosen;
    }

    public FrameGeneration Remembered { get; }

    public LsfgVkState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public bool Running
    {
        get
        {
            lock (_gate)
            {
                return _state == LsfgVkState.Loaded && _chosen.IsOn;
            }
        }
    }

    public bool RestartRequested => State == LsfgVkState.Restart;

    public string Notice => LsfgVkRelease.Notice;

    /// <summary>
    /// Before the window: with the player's Lossless Scaling found and the kept choice on, the layer made ready and
    /// loaded in this process; otherwise nothing is touched. What came of it is in <see cref="Describe"/>.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellation)
    {
        if (_lossless.Path is null)
        {
            Set(LsfgVkState.Unavailable, _lossless.Problem);

            return;
        }

        if (!Remembered.IsOn)
        {
            return;
        }

        var problem = await _installer.EnsureAsync(cancellation);

        if (problem is not null)
        {
            Set(LsfgVkState.Failed, problem);

            return;
        }

        problem = WriteConfig(Remembered);

        if (problem is not null)
        {
            Set(LsfgVkState.Failed, problem);

            return;
        }

        Load();
    }

    public void Use(FrameGeneration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _choices.Save(settings);
        LsfgVkState state;

        lock (_gate)
        {
            _chosen = settings;
            state = _state;
        }

        var preparable = state is LsfgVkState.Off or LsfgVkState.Failed;

        if (state == LsfgVkState.Loaded)
        {
            Apply(settings);
        }
        else if (settings.IsOn && preparable && TryBegin(state))
        {
            _ = Task.Run(PrepareAsync);
        }
    }

    public string Describe(string chosen)
    {
        LsfgVkState state;
        string? problem;

        lock (_gate)
        {
            state = _state;
            problem = _problem;
        }

        return state switch
        {
            LsfgVkState.Loaded when problem is not null => $"{Name}: {chosen} (its configuration could not be written: {problem})",
            LsfgVkState.Loaded => $"{Name}: {chosen}",
            LsfgVkState.Preparing => $"{Name}: downloading {LsfgVkRelease.Version}...",
            LsfgVkState.Restart => $"{Name}: ready, Nightcast restarts to load it",
            LsfgVkState.Unavailable => $"{Name} cannot run: {problem}",
            LsfgVkState.NotLoaded => $"{Name} did not load: {problem}",
            LsfgVkState.Failed => $"{Name} failed: {problem}",
            _ => $"off ({Name})",
        };
    }

    /// <summary>The variables set, then the loader asked; an older loader gets the data folder too, and is asked again.</summary>
    private void Load()
    {
        LsfgVkEnvironment.Apply(_environment, _folders);
        var check = _probe.Check();

        if (!check.Listed && check.Problem is null)
        {
            LsfgVkEnvironment.AddDataFolder(_environment, _folders);
            check = _probe.Check();
        }

        if (check.Loaded)
        {
            Set(LsfgVkState.Loaded, null);

            return;
        }

        Set(LsfgVkState.NotLoaded, Reason(check));
    }

    private string Reason(LayerCheck check)
    {
        if (check.Problem is not null)
        {
            return check.Problem;
        }

        if (check.Listed)
        {
            return $"the Vulkan loader lists the layer but it did not start (its log: {_folders.LogPath})";
        }

        return "the Vulkan loader did not find the layer";
    }

    /// <summary>A choice while loaded: written for the layer to take live; a failed write is said in the row.</summary>
    private void Apply(FrameGeneration settings)
    {
        var problem = WriteConfig(settings);

        lock (_gate)
        {
            _problem = problem;
        }
    }

    /// <summary>Into <see cref="LsfgVkState.Preparing"/> unless another thread got there first.</summary>
    private bool TryBegin(LsfgVkState seen)
    {
        lock (_gate)
        {
            if (_state != seen)
            {
                return false;
            }

            _state = LsfgVkState.Preparing;
            _problem = null;

            return true;
        }
    }

    /// <summary>In the background: made ready, then a restart asked for while the choice is still on.</summary>
    private async Task PrepareAsync()
    {
        var problem = await _installer.EnsureAsync(CancellationToken.None);
        FrameGeneration chosen;

        lock (_gate)
        {
            chosen = _chosen;
        }

        if (problem is null && chosen.IsOn)
        {
            problem = WriteConfig(chosen);
        }

        if (problem is not null)
        {
            Set(LsfgVkState.Failed, problem);
        }
        else if (chosen.IsOn)
        {
            Set(LsfgVkState.Restart, null);
        }
        else
        {
            Set(LsfgVkState.Off, null);
        }
    }

    /// <summary>The configuration for <paramref name="settings"/>; null when written, else why not.</summary>
    private string? WriteConfig(FrameGeneration settings)
    {
        try
        {
            Directory.CreateDirectory(_folders.Base);
            LsfgVkConfig.Save(_folders.ConfigPath, LsfgVkConfig.Text(settings, _lossless.Path!, _folders.LogPath));

            return null;
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            return failed.Message;
        }
    }

    private void Set(LsfgVkState state, string? problem)
    {
        lock (_gate)
        {
            _state = state;
            _problem = problem;
        }
    }
}
