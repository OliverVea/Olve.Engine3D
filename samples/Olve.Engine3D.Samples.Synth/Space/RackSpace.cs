using System.Numerics;
using Olve.Engine3D.Samples.Synth.Rack;

namespace Olve.Engine3D.Samples.Synth.Space;

/// <summary>A flat surface in the room that the rack's design units are drawn on: x right, y down, the normal out of it.</summary>
public readonly record struct Surface(Vector3 Origin, Vector3 Right, Vector3 Down, Vector3 Normal)
{
    public Vector3 ToWorld(Vector2 design) => Origin + Right * design.X + Down * design.Y;

    public Plane Plane => Plane.CreateFromVertices(Origin, Origin + Right, Origin + Down);

    /// <summary>The point of the surface at a world position on it.</summary>
    public Vector2 ToDesign(Vector3 world)
    {
        var offset = world - Origin;
        return new Vector2(Vector3.Dot(offset, Right) / Right.LengthSquared(), Vector3.Dot(offset, Down) / Down.LengthSquared());
    }

    /// <summary>
    /// Design units to world, as rows: the right and down steps of a design unit, the unit normal, and the origin lifted
    /// <paramref name="lift"/> metres off the surface.
    /// </summary>
    public Matrix4x4 ToMatrix(float lift = 0)
    {
        var origin = Origin + Normal * lift;
        return new Matrix4x4(
            Right.X, Right.Y, Right.Z, 0,
            Down.X, Down.Y, Down.Z, 0,
            Normal.X, Normal.Y, Normal.Z, 0,
            origin.X, origin.Y, origin.Z, 1);
    }
}

/// <summary>The size of a scope's CRT cabinet, in metres: the screen's opening and the bezel round it.</summary>
public static class CrtCabinet
{
    public const float ScreenWidth = 0.2f, ScreenHeight = 0.15f;

    /// <summary>The bezel's sides, top and bottom, and how far it stands in front of the glass; the bottom is deep enough for its controls.</summary>
    public const float BezelSide = 0.035f, BezelTop = 0.03f, BezelBottom = 0.065f, BezelDepth = 0.03f;

    public const float Width = ScreenWidth + 2 * BezelSide, Height = ScreenHeight + BezelTop + BezelBottom;

    /// <summary>How far the cabinet's centre is below the screen's, as its bottom bezel is deeper than its top.</summary>
    public const float Drop = (BezelBottom - BezelTop) / 2;

    /// <summary>The input jack on the bottom bezel's face, in its design units from its top left.</summary>
    public static readonly Vector2 Jack = new(305, BezelBottom / RackSpace.Scale / 2 - 8);
}

/// <summary>A CRT scope's cabinet: where its screen's centre is and which way the screen faces.</summary>
public readonly record struct ScopePlacement(Vector3 Centre, Vector3 Facing)
{
    /// <summary>The screen's axes: right, up, and out toward the seat.</summary>
    public (Vector3 X, Vector3 Y, Vector3 Z) Axes
    {
        get
        {
            var x = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, Facing));
            return (x, Vector3.Cross(Facing, x), Facing);
        }
    }

    /// <summary>The bottom bezel's face, under the screen, where its label, knobs and jack are.</summary>
    public Surface Bezel
    {
        get
        {
            var (x, y, z) = Axes;
            return new Surface(Centre - y * CrtCabinet.ScreenHeight / 2 - x * CrtCabinet.Width / 2, x * RackSpace.Scale, -y * RackSpace.Scale, z);
        }
    }

    /// <summary>Where the input jack is, on the bezel's face.</summary>
    public Vector3 Jack => Bezel.ToWorld(CrtCabinet.Jack);
}

/// <summary>
/// How the seat sees the side case once turned to it, in radians: how far down it looks, at the face's middle, and how
/// much it then takes in across and from top to bottom at least, the face with a little room round it.
/// </summary>
public readonly record struct SideFrame(float Pitch, float Across, float High);

/// <summary>A part of the layout drawn on a surface of its own: the panels, the keys, the keyboard's cheek, the LIGHTS box.</summary>
public readonly record struct Region(Box Area, Surface Surface);

/// <summary>
/// Where the rack sits in the room, in metres, Y up, the player facing -Z: the panels on a face leaning back from upright,
/// framed by the case's raised rim, the keybed in front of it with the keyboard's cheek left of the keys, a CRT scope over
/// each top corner tilted down at the player, a light by each scope, the LIGHTS box on the desk right of the keybed,
/// turned to the seat, a key light over the seat, and the seat. Left of the seat, a quarter turn round, the side case
/// stands on the desk's return: a low case for the sequencer and its quantizer, its face sloping up away from the seat
/// and facing it. The panels keep their layout from <see cref="RackLayout"/>,
/// a design unit to <see cref="Scale"/> metres, the case's middle at x = 0; the keys are deeper than they are drawn, as a
/// real keyboard's are. On a portrait screen the scopes stand side by side over the case's top edge instead, so the
/// rack can fill the screen's width (<see cref="Portrait"/>).
/// </summary>
public sealed class RackSpace
{
    /// <summary>Metres per design unit.</summary>
    public const float Scale = 0.00066f;

    /// <summary>How far the panels lean back from upright, in radians.</summary>
    public const float Tilt = 0.45f;

    /// <summary>How much deeper a key is than it is drawn.</summary>
    public const float KeyDepth = 1.8f;

    /// <summary>The panels' bottom edge: its height and how far in front of the player.</summary>
    private static readonly Vector2 PanelsBottom = new(0.16f, -0.44f);

    /// <summary>The keys' top: their height, and how far the keys' back edge is.</summary>
    private static readonly Vector2 KeysBack = new(0.07f, -0.41f);

    /// <summary>The height of the cheeks' tops, a little over the keys.</summary>
    public const float CheekTop = 0.082f;

    /// <summary>How far the LIGHTS box's face leans back from flat, in radians, and how high its front edge is.</summary>
    private const float DeskSlope = 0.62f, DeskFront = 0.035f;

    public static readonly Vector3 Eye = new(0, 0.61f, 0.29f);

    /// <summary>
    /// Where the eye is on a portrait screen: further back and higher, so the case's top, further off, is less smaller than
    /// the keys in front, and both can fill the narrow screen's width.
    /// </summary>
    public static readonly Vector3 PortraitEye = new(0, 1.0f, 0.55f);

    /// <summary>Where the seat's eye is in this room.</summary>
    public Vector3 SeatEye => Portrait ? PortraitEye : Eye;

    /// <summary>The warm key light, above the seat and a little behind it.</summary>
    public static readonly Vector3 KeyLight = new(0.15f, 1.35f, 0.55f);

    /// <summary>How wide the case's rim around the panels is, in design units.</summary>
    public const float Rim = 14;

    /// <summary>Where the seat looks when the head is still: down at the rack, in radians.</summary>
    public const float RestPitch = 0.29f;

    /// <summary>How far apart the two scopes stand over the case on a portrait screen, in metres, and how far their cabinets' bottoms are over its top edge.</summary>
    private const float PortraitScopeGap = 0.02f, PortraitScopeLift = 0.035f;


    /// <summary>How far left the seat turns to face the side case, in radians: a quarter turn.</summary>
    public const float SideYaw = MathF.PI / 2;

    /// <summary>
    /// The side case's face, as seen turned to it: its bottom edge's height and how far ahead of the eye it is, and how far
    /// it leans back from upright, in radians; it is centred on the seat's turned view.
    /// </summary>
    private static readonly Vector2 SideBottom = new(0.1f, -0.36f);
    public const float SideTilt = 0.6f;


    public RackSpace(RackLayout layout, bool portrait = false)
    {
        Layout = layout;
        Portrait = portrait;
        PianoTop = layout.PianoTop;
        PanelsHeight = PianoTop - RackLayout.PianoGap;
        Width = layout.CaseWidth * Scale;

        var down = new Vector3(0, -MathF.Cos(Tilt), MathF.Sin(Tilt));
        var normal = new Vector3(0, MathF.Sin(Tilt), MathF.Cos(Tilt));
        var bottomLeft = new Vector3(-Width / 2, PanelsBottom.X, PanelsBottom.Y);
        Panels = new Surface(bottomLeft - down * PanelsHeight * Scale, Vector3.UnitX * Scale, down * Scale, normal);

        // The keybed: design y runs from the keys' back edge toward the player
        Keys = new Surface(new Vector3(-Width / 2, KeysBack.X, KeysBack.Y - PianoTop * Scale * KeyDepth),
            Vector3.UnitX * Scale, Vector3.UnitZ * Scale * KeyDepth, Vector3.UnitY);
        Cheek = new Surface(new Vector3(-Width / 2, CheekTop, KeysBack.Y - PianoTop * Scale), Vector3.UnitX * Scale, Vector3.UnitZ * Scale, Vector3.UnitY);

        Face = new Box(new Vector2(RackLayout.Margin - Rim, RackLayout.RackTop - Rim),
            new Vector2(layout.CaseWidth - 2 * (RackLayout.Margin - Rim), PanelsHeight - RackLayout.RackTop + 2 * Rim));
        var topLeft = Panels.ToWorld(Face.Min);
        var topRight = Panels.ToWorld(new Vector2(Face.Max.X, Face.Min.Y));
        var topMiddle = (topLeft + topRight) / 2;
        Scopes = portrait
            ? [PortraitScope(topMiddle, side: -1), PortraitScope(topMiddle, side: 1)]
            : [Scope(topLeft, side: -1), Scope(topRight, side: 1)];
        Lights = [Scopes[0].Centre + new Vector3(-0.23f, 0.07f, 0.1f), Scopes[1].Centre + new Vector3(0.23f, 0.07f, 0.1f)];

        var regions = new List<Region>
        {
            new(new Box(Vector2.Zero, new Vector2(layout.CaseWidth, PanelsHeight)), Panels),
            new(layout.Keys, Keys),
        };
        if (layout.Cheek is { } cheek)
        {
            regions.Add(new Region(cheek.Bounds, Cheek));
        }

        if (layout.Desk is { } desk)
        {
            Desk = DeskSurface(desk.Bounds);
            regions.Add(new Region(desk.Bounds, Desk.Value));
        }

        if (layout.Side is { } side)
        {
            Side = SideSurface(side);
            regions.Add(new Region(side, Side.Value));
            var centre = Side.Value.ToWorld(side.Centre) - Eye;
            var distance = centre.Length();
            // Its width with a little room either side, and its height, as it leans away from the eye, with more
            var size = side.Size * Scale;
            SideFrame = new SideFrame(
                MathF.Atan2(-centre.Y, new Vector2(centre.X, centre.Z).Length()),
                2 * MathF.Atan(size.X / 2 * 1.12f / distance),
                2 * MathF.Atan(size.Y / 2 * 1.5f / distance));
        }

        Regions = regions;
    }

    public RackLayout Layout { get; }

    /// <summary>Whether the room is laid out for a portrait screen: the scopes over the case's top edge rather than its corners.</summary>
    public bool Portrait { get; }

    /// <summary>
    /// What the seat should take in: the case's face, the keys and the keyboard's cheek, and each scope's cabinet. The
    /// LIGHTS box on the desk is left out, as it may run off the side.
    /// </summary>
    public IEnumerable<Vector3> Framed =>
        Corners(Face).Select(Panels.ToWorld)
            .Concat(Corners(Layout.Keys).Select(Keys.ToWorld))
            .Concat(Layout.Cheek is { } cheek ? Corners(cheek.Bounds).Select(Cheek.ToWorld) : [])
            .Concat(Scopes.SelectMany(s =>
            {
                var (x, y, _) = s.Axes;
                var top = s.Centre + y * (CrtCabinet.ScreenHeight / 2 + CrtCabinet.BezelTop);
                var bottom = s.Centre - y * (CrtCabinet.ScreenHeight / 2 + CrtCabinet.BezelBottom);
                var half = x * CrtCabinet.Width / 2;
                return new[] { top - half, top + half, bottom - half, bottom + half };
            }));

    private static IEnumerable<Vector2> Corners(Box box) => [box.Min, new(box.Max.X, box.Min.Y), new(box.Min.X, box.Max.Y), box.Max];

    /// <summary>The case's width in metres.</summary>
    public float Width { get; }

    /// <summary>The design y of the piano's top edge, where the panels' design area ends.</summary>
    public float PianoTop { get; }

    /// <summary>The design height of the panels' area.</summary>
    public float PanelsHeight { get; }

    /// <summary>The case's face in design units: the panels' rows and the rim round them.</summary>
    public Box Face { get; }

    /// <summary>The face the panels are on.</summary>
    public Surface Panels { get; }

    /// <summary>The top of the keys, at rest.</summary>
    public Surface Keys { get; }

    /// <summary>The top of the keyboard's cheek, left of the keys.</summary>
    public Surface Cheek { get; }

    /// <summary>The LIGHTS box's face, if the patch has the module.</summary>
    public Surface? Desk { get; }

    /// <summary>The side case's face, if the patch has a sequencer or a quantizer.</summary>
    public Surface? Side { get; }

    /// <summary>How the seat sees the side case when turned to it.</summary>
    public SideFrame SideFrame { get; } = new(RestPitch, 1, 1);

    /// <summary>Each part of the layout with the surface it is drawn on.</summary>
    public IReadOnlyList<Region> Regions { get; }

    /// <summary>The left scope, then the right.</summary>
    public IReadOnlyList<ScopePlacement> Scopes { get; }

    /// <summary>The left light, then the right.</summary>
    public IReadOnlyList<Vector3> Lights { get; }

    /// <summary>The scope's screen sits over the corner, tilted down and in toward the seat.</summary>
    private static ScopePlacement Scope(Vector3 corner, float side)
    {
        var centre = corner + new Vector3(side * 0.11f, 0.09f, -0.05f);
        return new ScopePlacement(centre, Vector3.Normalize(Eye - centre));
    }

    /// <summary>On a portrait screen, the scope's cabinet stands over the case's top edge, beside the other, tilted down at the seat.</summary>
    private static ScopePlacement PortraitScope(Vector3 topMiddle, float side)
    {
        var centre = topMiddle + new Vector3(side * (CrtCabinet.Width + PortraitScopeGap) / 2,
            CrtCabinet.ScreenHeight / 2 + CrtCabinet.BezelBottom + PortraitScopeLift, -0.05f);
        return new ScopePlacement(centre, Vector3.Normalize(PortraitEye - centre));
    }

    // The LIGHTS box stands on the desk right of the keybed, its face sloping up away from the seat and turned toward it
    private Surface DeskSurface(Box bounds)
    {
        var size = bounds.Size * Scale;
        var frontLeft = new Vector3(Width / 2 + RackLayout.DeskGap * Scale, DeskFront, KeysBack.Y + RackLayout.CheekDepth * Scale);
        var middle = frontLeft + new Vector3(size.X / 2, 0, 0);
        var yaw = MathF.Atan2(Eye.X - middle.X, Eye.Z - middle.Z);
        var right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
        var forward = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
        var down = forward * MathF.Cos(DeskSlope) - Vector3.UnitY * MathF.Sin(DeskSlope);
        var normal = Vector3.UnitY * MathF.Cos(DeskSlope) + forward * MathF.Sin(DeskSlope);
        // Turned about the front edge's middle, which stays put
        var frontMiddle = middle;
        var bottomLeft = frontMiddle - right * size.X / 2;
        var topLeft = bottomLeft - down * size.Y;
        return new Surface(topLeft - right * bounds.Min.X * Scale - down * bounds.Min.Y * Scale, right * Scale, down * Scale, normal);
    }

    /// <summary>A point or a direction seen straight ahead turned a quarter left round the eye, as the side case is placed.</summary>
    public static Vector3 TurnedLeft(Vector3 point, bool isDirection = false)
    {
        var (sin, cos) = MathF.SinCos(SideYaw);
        var from = isDirection ? Vector3.Zero : Eye;
        var p = point - from;
        return from + new Vector3(p.X * cos + p.Z * sin, p.Y, -p.X * sin + p.Z * cos);
    }

    // The side case's face: laid out as if straight ahead, its bottom edge in front of the eye and leaning back, then turned left round the eye
    private static Surface SideSurface(Box face)
    {
        var right = Vector3.UnitX * Scale;
        var down = new Vector3(0, -MathF.Cos(SideTilt), MathF.Sin(SideTilt)) * Scale;
        var normal = new Vector3(0, MathF.Sin(SideTilt), MathF.Cos(SideTilt));
        var bottomLeft = new Vector3(-face.Size.X * Scale / 2, SideBottom.X, Eye.Z + SideBottom.Y);
        var origin = bottomLeft - right * face.Min.X - down * face.Max.Y;
        return new Surface(TurnedLeft(origin), TurnedLeft(right, true), TurnedLeft(down, true), TurnedLeft(normal, true));
    }

    /// <summary>The surface a point of the layout is drawn on: a scope's bezel round its jack, else the part of the layout it is in, else the panels or the keys.</summary>
    public Surface SurfaceAt(Vector2 design)
    {
        foreach (var scope in Layout.Scopes)
        {
            if (Vector2.Distance(design, scope.In.Centre) < 4 * RackLayout.JackRadius && scope.Index < Scopes.Count)
            {
                // The bezel moved so the jack's design point lands on the jack
                var bezel = Scopes[scope.Index].Bezel;
                return bezel with { Origin = bezel.Origin + bezel.Right * (CrtCabinet.Jack.X - scope.In.Centre.X) + bezel.Down * (CrtCabinet.Jack.Y - scope.In.Centre.Y) };
            }
        }

        foreach (var region in Regions)
        {
            if (region.Area.Contains(design))
            {
                return region.Surface;
            }
        }

        return design.Y >= PianoTop - RackLayout.PianoGap / 2 ? Keys : Panels;
    }

    /// <summary>The scope whose input jack a ray passes within reach of, nearest first.</summary>
    public ScopeLayout? ScopeJackOnRay(Vector3 origin, Vector3 direction, float reach = 0.012f)
    {
        direction = Vector3.Normalize(direction);
        return Layout.Scopes
            .Where(s => s.Index < Scopes.Count)
            .Select(s => (Scope: s, Along: Vector3.Dot(Scopes[s.Index].Jack - origin, direction), Jack: Scopes[s.Index].Jack))
            .Where(s => s.Along > 0 && Vector3.Distance(origin + direction * s.Along, s.Jack) <= reach)
            .OrderBy(s => s.Along)
            .Select(s => s.Scope)
            .FirstOrDefault();
    }
}
