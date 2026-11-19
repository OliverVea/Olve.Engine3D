using Olve.Engine3D.Audio.Devices;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// How <see cref="DesktopHost"/> runs the game.
/// </summary>
public record DesktopHostOptions
{
    public string AppName { get; init; } = "";

    /// <summary>Names the command pipe; a random one is used when null.</summary>
    public string? InstanceId { get; init; }

    /// <summary>Accept commands over the command pipe.</summary>
    public bool Listen { get; init; }

    /// <summary>How frames are advanced.</summary>
    public FrameLoop FrameLoop { get; init; } = FrameLoop.RealTime;

    /// <summary>Fixed borderless window size, overriding <see cref="DesktopWindowOptions.Size"/>; none when null.</summary>
    public Vector2<int>? Resolution { get; init; }

    public string? UserStorageRoot { get; init; }

    public string? FileExchangeFolder { get; init; }

    public DesktopWindowOptions Window { get; init; } = new();

    /// <summary>The audio outputs opened at start (<see cref="AudioConfiguration"/>).</summary>
    public AudioStartupOptions Audio { get; init; } = AudioStartupOptions.Default;
}

/// <summary>
/// The window <see cref="DesktopHost"/> opens.
/// </summary>
public record DesktopWindowOptions
{
    public string Title { get; init; } = "";

    /// <summary>Content area size, in screen coordinates.</summary>
    public Vector2<int> Size { get; init; } = new(1280, 720);

    /// <summary>Multisample anti-aliasing samples; 0 turns it off.</summary>
    public int Samples { get; init; }

    public bool VSync { get; init; } = true;
}
