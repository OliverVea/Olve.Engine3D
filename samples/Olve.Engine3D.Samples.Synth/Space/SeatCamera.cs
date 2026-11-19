using System.Numerics;
using Olve.Engine3D.Camera;
using Olve.Engine3D.Camera.Projections;
using Olve.Engine3D.Camera.Views;

namespace Olve.Engine3D.Samples.Synth.Space;

/// <summary>
/// The player's eyes, seated at <see cref="RackSpace.Eye"/>: the head turns a little toward the pointer, up to
/// <see cref="MaxYaw"/> each side and <see cref="MaxPitch"/> up or down, easing there, and back to rest without one.
/// On a portrait screen, with a room laid out for one (<see cref="RackSpace.Portrait"/>), the view is fitted to what the
/// seat takes in (<see cref="RackSpace.Framed"/>) instead: across the screen's width, looking down at its middle. The
/// seat turns a quarter left to the side case (<see cref="Turned"/>), easing round, and looks down at it there from
/// <see cref="RackSpace.Eye"/> on any screen; the head still follows the pointer on top of that.
/// </summary>
public sealed class SeatCamera
{
    public const float MaxYaw = 8 * float.Pi / 180;
    public const float MaxPitch = 5 * float.Pi / 180;

    /// <summary>The vertical field of view on a 16:9 screen, in radians. The view keeps the width it has there, so a narrower screen sees more above and below, and a wider one, such as a phone's held sideways, less.</summary>
    public const float FieldOfView = 1.02f;

    /// <summary>The least the view takes in from top to bottom, in radians, however wide the screen: the scopes over the keybed.</summary>
    public const float MinFieldOfView = 1.02f;

    /// <summary>How wide the view is across, in radians: a 16:9 screen's at <see cref="FieldOfView"/>.</summary>
    private static readonly float Across = 2 * MathF.Atan(MathF.Tan(FieldOfView / 2) * 16 / 9);

    /// <summary>How quickly the head follows, per second.</summary>
    private const float Follow = 6;

    /// <summary>How much of a portrait screen, from its middle to its edges, what the seat takes in fills.</summary>
    public const float PortraitFill = 0.97f;

    /// <summary>
    /// How the height a portrait screen has to spare is shared: this much of it under what the seat takes in, and the
    /// rest over it, where the window and the city are, rather than the floor under the desk.
    /// </summary>
    public const float PortraitBelow = 0.35f;

    // Where the head rests, and the space and aspect it was fitted to on a portrait screen
    private float _restPitch = RackSpace.RestPitch;
    private Vector3 _rackEye = RackSpace.Eye;
    private float _rackFieldOfView = FieldOfView;
    private (RackSpace Space, float Aspect, float Pitch, float FieldOfView)? _portrait;

    public Camera<FirstPersonView, PerspectiveProjection> Camera { get; } = Engine3D.Camera.Camera.Create(
        new FirstPersonView { Position = RackSpace.Eye },
        new PerspectiveProjection { FieldOfView = FieldOfView, NearPlane = 0.05f, FarPlane = 20 });

    /// <summary>The head's turn from rest: yaw to the right, pitch down, in radians.</summary>
    public Vector2 Look { get; private set; }

    /// <summary>Whether the seat is turned to the side case, or is turning there.</summary>
    public bool Turned { get; set; }

    /// <summary>How far round to the side case the seat is, from 0 facing the rack to 1 facing the side case.</summary>
    public float Turn { get; private set; }

    /// <summary>How the seat sees the side case once turned to it (<see cref="RackSpace.SideFrame"/>).</summary>
    public SideFrame Side { get; set; } = new(RackSpace.RestPitch, Across, MinFieldOfView);

    public SeatCamera() => Apply();

    /// <param name="pointer">Where the head looks toward, from -1 to 1 across the screen (y down), or null to rest.</param>
    /// <param name="space">The room, if it is laid out yet; a portrait one is framed to fit.</param>
    public void Update(Vector2? pointer, float aspect, TimeSpan elapsed, RackSpace? space = null)
    {
        var target = pointer is { } p ? Vector2.Clamp(p, -Vector2.One, Vector2.One) * new Vector2(MaxYaw, MaxPitch) : Vector2.Zero;
        var ease = 1 - MathF.Exp(-Follow * (float)elapsed.TotalSeconds);
        Look += (target - Look) * ease;
        Turn += ((Turned ? 1 : 0) - Turn) * ease;
        if (MathF.Abs(Turn - (Turned ? 1 : 0)) < 1e-4f)
        {
            Turn = Turned ? 1 : 0;
        }

        Camera.Projection.AspectRatio = aspect;
        if (aspect < 1 && space is { Portrait: true })
        {
            if (_portrait is not { } fit || fit.Space != space || fit.Aspect != aspect)
            {
                var (pitch, fieldOfView) = FitPortrait(space, aspect);
                _portrait = fit = (space, aspect, pitch, fieldOfView);
            }

            _restPitch = fit.Pitch;
            _rackEye = space.SeatEye;
            _rackFieldOfView = fit.FieldOfView;
        }
        else
        {
            _restPitch = RackSpace.RestPitch;
            _rackEye = RackSpace.Eye;
            _rackFieldOfView = MathF.Max(MinFieldOfView, 2 * MathF.Atan(MathF.Tan(Across / 2) / aspect));
        }

        // Turned to the side case, the view closes in to frame it, as the seat leans toward it, from the eye the side case
        // is placed round
        var turn = Smooth(Turn);
        var side = MathF.Max(Side.High, 2 * MathF.Atan(MathF.Tan(Side.Across / 2) / aspect));
        Camera.Projection.FieldOfView = _rackFieldOfView + (side - _rackFieldOfView) * turn;
        Camera.View.Position = Vector3.Lerp(_rackEye, RackSpace.Eye, turn);
        Apply();
    }

    private static float Smooth(float x) => x * x * (3 - 2 * x);

    private void Apply()
    {
        // Facing -Z at rest; turning right swings forward toward +X, pitching down toward -Y. The turn to the side case
        // eases in and out, so the seat sets off and settles gently
        var turn = Smooth(Turn);
        var pitch = _restPitch + (Side.Pitch - _restPitch) * turn;
        Camera.View.Rotation = Rest(pitch + Look.Y, Look.X - RackSpace.SideYaw * turn);
    }

    private static Matrix4x4 Rest(float pitch, float yaw) => Matrix4x4.CreateRotationX(pitch) * Matrix4x4.CreateRotationY(float.Pi - yaw);

    /// <summary>
    /// The vertical field of view that fits what the seat takes in across the screen's width, or top to bottom where it
    /// is taller than the screen, and the pitch that leaves <see cref="PortraitBelow"/> of the height to spare under it.
    /// </summary>
    private static (float Pitch, float FieldOfView) FitPortrait(RackSpace space, float aspect)
    {
        var points = space.Framed.ToArray();
        var view = new FirstPersonView { Position = space.SeatEye };
        // Each point's slope off the line of sight, across and up
        Vector2[] Slopes(float pitch)
        {
            view.Rotation = Rest(pitch, 0);
            var matrix = view.GetViewMatrix();
            return [.. points.Select(p => Vector3.Transform(p, matrix)).Select(v => new Vector2(v.X, v.Y) / MathF.Abs(v.Z))];
        }

        // Looking further down raises everything on the screen, so halve the range until its middle is at the height asked
        float PitchFor(float middle)
        {
            float low = -0.2f, high = 1.2f;
            for (var i = 0; i < 40; i++)
            {
                var pitch = (low + high) / 2;
                var slopes = Slopes(pitch);
                if ((slopes.Max(s => s.Y) + slopes.Min(s => s.Y)) / 2 > middle)
                {
                    high = pitch;
                }
                else
                {
                    low = pitch;
                }
            }

            return (low + high) / 2;
        }

        (float Tan, float Half) Fit(float pitch)
        {
            var slopes = Slopes(pitch);
            var extent = slopes.Aggregate(Vector2.Zero, (max, s) => Vector2.Max(max, Vector2.Abs(s))) / PortraitFill;
            return (MathF.Max(extent.Y, extent.X / aspect), (slopes.Max(s => s.Y) - slopes.Min(s => s.Y)) / 2);
        }

        // Fitted in the middle first, then lowered into the height to spare and fitted again
        var (tan, half) = Fit(PitchFor(0));
        var lowered = PitchFor(-MathF.Max(0, tan - half) * (1 - 2 * PortraitBelow));
        return (lowered, 2 * MathF.Atan(Fit(lowered).Tan));
    }
}
