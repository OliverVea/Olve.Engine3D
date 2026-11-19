using System.Numerics;
using Olve.Engine3D.Input;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Time;

namespace Olve.Engine3D.Samples.Synth.Space;

/// <summary>
/// The seat in front of the rack: each frame, before the rack's input, the head follows the mouse a little, and pointers
/// reach the rack through the camera. The head stays still until the mouse moves, and a touch stops it following, as a
/// finger is there to press something; so a mouse that never moved, or the mouse a touch falls back to, doesn't turn it.
/// With a side case, Tab, a press on the on-screen arrow (<see cref="TurnArrow"/>), or the mouse held on it a moment,
/// turns the seat to the side case and back. The arrow takes the press: the rack doesn't see it.
/// </summary>
public sealed class SeatService(InputState input, IWindowInfo window, DeltaTimeService time) : ISceneService, IRackViews
{
    public SeatCamera Seat { get; } = new();

    public RackSpace? Space { get; private set; }

    /// <summary>The on-screen arrow that turns the seat, in pixels, this frame: none without a side case.</summary>
    public Box? Arrow { get; private set; }

    /// <summary>How long the mouse has rested on the arrow, as a share of <see cref="TurnArrow.DwellSeconds"/>.</summary>
    public float Dwell { get; private set; }

    private bool _mouseLooks;
    private bool _dwelt;

    public int Priority => -1;

    public Result Input()
    {
        if (input.MainPointer != Pointer.Mouse)
        {
            _mouseLooks = false;
        }
        else if (input.GetDelta(Pointer.Mouse.Position) != Vector2.Zero)
        {
            _mouseLooks = true;
        }

        var size = window.ContentAreaSize;
        var aspect = size.Y > 0 ? size.X / (float)size.Y : 1;
        Vector2? pointer = _mouseLooks ? input.GetNormalizedPosition(Pointer.Mouse) : null;
        Turn(new Vector2(int.Max(1, size.X), int.Max(1, size.Y)));
        Seat.Update(pointer, aspect, time.RawDeltaTime, Space);
        return Result.Success();
    }

    // Tab, a press on the arrow or the mouse resting on it turns the seat. The arrow stays where it was for the rest of the
    // frame, so the press that turned it doesn't reach the rack where the arrow was.
    private void Turn(Vector2 screen)
    {
        if (Space?.Side is null)
        {
            Arrow = null;
            Seat.Turned = false;
            return;
        }

        var arrow = TurnArrow.Bounds(screen, Seat.Turned);
        Arrow = arrow;
        var turn = input.WasPressed(Key.Tab);
        foreach (var pointer in input.Pointers)
        {
            turn |= input.WasPressed(pointer.Primary) && arrow.Contains(input.GetPosition(pointer));
        }

        if (_mouseLooks && arrow.Contains(input.GetPosition(Pointer.Mouse)))
        {
            Dwell = float.Min(1, Dwell + (float)time.RawDeltaTime.TotalSeconds / TurnArrow.DwellSeconds);
            turn |= Dwell >= 1 && !_dwelt;
            _dwelt |= Dwell >= 1;
        }
        else
        {
            Dwell = 0;
            _dwelt = false;
        }

        if (turn)
        {
            Seat.Turned = !Seat.Turned;
            Dwell = 0;
        }

        Seat.Side = Space.SideFrame;
    }

    public IRackView Fit(RackLayout layout, Vector2 screen)
    {
        // A portrait screen has its own room, with the scopes over the case; turning the screen lays it out again
        var portrait = screen.X < screen.Y;
        if (Space?.Layout != layout || Space.Portrait != portrait)
        {
            Space = new RackSpace(layout, portrait);
        }

        return new SeatRackView(Seat, Space, screen) { Arrow = Arrow };
    }
}

/// <summary>
/// The arrow on the screen's edge that turns the seat: on the left, pointing to the side case, while the seat faces the
/// rack, and on the right, pointing back, while it faces the side case; half way down, a finger's width and more. On a
/// portrait screen, whose view fills its width with the rack or the side case, it sits higher, by the window, where
/// there is room to spare and no control under it.
/// </summary>
public static class TurnArrow
{
    /// <summary>How long the mouse rests on the arrow to turn the seat, in seconds.</summary>
    public const float DwellSeconds = 0.6f;

    /// <summary>The arrow's place on a screen of this size, in pixels.</summary>
    public static Box Bounds(Vector2 screen, bool turned)
    {
        var height = float.Clamp(float.Min(screen.X, screen.Y) * 0.12f, 44, 84);
        var width = height * 0.7f;
        var x = turned ? screen.X - width - Inset : Inset;
        var middle = screen.Y * (screen.X < screen.Y ? PortraitHeight : 0.5f);
        return new Box(new Vector2(x, middle - height / 2), new Vector2(width, height));
    }

    private const float Inset = 6;

    // How far down a portrait screen the arrow's middle is
    private const float PortraitHeight = 0.3f;
}
