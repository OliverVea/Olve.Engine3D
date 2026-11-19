using Olve.Engine3D.Input;

namespace Olve.Engine3D;

/// <summary>
/// Something the platform reported between two frames: input, or a change to the window or the audio output.
/// </summary>
public abstract record PlatformEvent;

public sealed record ButtonDownEvent(Button Button) : PlatformEvent;

public sealed record ButtonUpEvent(Button Button) : PlatformEvent;

/// <param name="Value">The axis's new value for an <see cref="AxisKind.Absolute"/> axis, or the change for an
/// <see cref="AxisKind.Relative"/> one.</param>
public sealed record AxisEvent(InputAxis Axis, Vector2 Value) : PlatformEvent;

/// <summary>A pointer came into being, such as a finger touching the screen.</summary>
public sealed record PointerAddedEvent(Pointer Pointer) : PlatformEvent;

/// <summary>A pointer went away, such as a finger lifted; report its last button up first.</summary>
public sealed record PointerRemovedEvent(Pointer Pointer) : PlatformEvent;

/// <param name="ContentAreaSize">The window's new content area size, in pixels.</param>
public sealed record ResizedEvent(Vector2<int> ContentAreaSize) : PlatformEvent;
