using System.Numerics;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Samples.Synth.Space;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class SeatTests
{
    private static readonly RackLayout Layout = new(Presets.Classic());
    private static readonly RackSpace Space = new(Layout);

    private static Vector2 Ndc(SeatCamera seat, Vector3 world)
    {
        var clip = Vector4.Transform(new Vector4(world, 1), seat.Camera.GetViewMatrix() * seat.Camera.GetProjectionMatrix());
        return new Vector2(clip.X, clip.Y) / clip.W;
    }

    private static bool InView(Vector2 ndc) => MathF.Abs(ndc.X) < 0.95f && MathF.Abs(ndc.Y) < 0.95f;

    private static IEnumerable<Vector2> Corners(Box box) => [box.Min, new(box.Max.X, box.Min.Y), new(box.Min.X, box.Max.Y), box.Max];

    [Test]
    [Arguments(16f / 9, RackLayout.WideOctaves)]
    [Arguments(2.1f, RackLayout.WideOctaves)]
    [Arguments(4f / 3, RackLayout.WideOctaves)]
    [Arguments(16f / 9, RackLayout.NarrowOctaves)]
    [Arguments(2.22f, RackLayout.NarrowOctaves)]
    [Arguments(4f / 3, RackLayout.NarrowOctaves)]
    public async Task Looking_straight_ahead_shows_both_scopes_screens_every_panel_and_the_keybed(float aspect, int octaves)
    {
        var layout = new RackLayout(Presets.Classic(), octaves);
        var space = new RackSpace(layout);
        var seat = new SeatCamera();
        seat.Update(null, aspect, TimeSpan.Zero);

        // The LIGHTS box on the desk beside the keybed may run off the edge; the rack itself may not. The side case is behind the turn
        var points = space.Regions.Where(r => r.Surface != space.Desk && r.Surface != space.Side).SelectMany(r => Corners(r.Area).Select(r.Surface.ToWorld))
            .Concat(space.Scopes.SelectMany(s =>
            {
                // The screen's sides, and its glass's top corners
                var top = s.Centre + s.Axes.Y * CrtCabinet.ScreenHeight / 2;
                return new[]
                {
                    s.Centre - s.Axes.X * CrtCabinet.ScreenWidth / 2, s.Centre + s.Axes.X * CrtCabinet.ScreenWidth / 2,
                    top - s.Axes.X * CrtCabinet.ScreenWidth / 2, top + s.Axes.X * CrtCabinet.ScreenWidth / 2,
                };
            }));

        await Assert.That(space.Regions.Count).IsEqualTo(5);
        foreach (var point in points)
        {
            await Assert.That(InView(Ndc(seat, point))).IsTrue().Because($"{point} is out of view: {Ndc(seat, point)}");
        }

        // Sitting at the desk: the rack nearly fills the screen across, or top to bottom where the screen is wide
        var extent = points.Select(p => Vector2.Abs(Ndc(seat, p))).Aggregate(Vector2.Max);
        await Assert.That(MathF.Max(extent.X, extent.Y)).IsGreaterThan(0.87f).Because($"the rack fills only {extent} of the view");
    }

    // A phone held upright, a tall phone, and a portrait monitor wide enough for four octaves
    [Test]
    [Arguments(412f / 839, RackLayout.NarrowOctaves)]
    [Arguments(9f / 20, RackLayout.NarrowOctaves)]
    [Arguments(9f / 16, RackLayout.WideOctaves)]
    [Arguments(3f / 4, RackLayout.WideOctaves)]
    public async Task On_a_portrait_screen_the_case_and_keybed_fill_the_width_under_the_scopes(float aspect, int octaves)
    {
        var layout = new RackLayout(Presets.Classic(), octaves);
        var space = new RackSpace(layout, portrait: true);
        var seat = new SeatCamera();
        seat.Update(null, aspect, TimeSpan.Zero, space);

        // Everything the seat takes in is on the screen: the case, the keys, the cheek and both scopes' cabinets
        var framed = space.Framed.Select(p => Ndc(seat, p)).ToList();
        foreach (var point in framed)
        {
            await Assert.That(MathF.Abs(point.X) <= 0.98f && MathF.Abs(point.Y) <= 0.98f).IsTrue().Because($"{point} is out of view");
        }

        // The case reaches nearly from edge to edge, and what is framed spans more of the screen's height the less tall it is
        var face = new[] { space.Face.Min, new Vector2(space.Face.Max.X, space.Face.Min.Y) }.Select(c => Ndc(seat, space.Panels.ToWorld(c))).ToList();
        await Assert.That(face.Max(c => MathF.Abs(c.X))).IsGreaterThan(0.8f).Because($"the case's top reaches only {face[0]} to {face[1]}");
        var (top, bottom) = (framed.Max(p => p.Y), framed.Min(p => p.Y));
        await Assert.That(top - bottom).IsGreaterThan(1.5f * aspect).Because($"what is framed spans {bottom} to {top}");
        // What it leaves to spare is mostly over it, where the window is
        await Assert.That(1 + bottom).IsLessThan(1 - top).Because($"what is framed spans {bottom} to {top}");

        // The scopes stand side by side over the case, their screens fully in view
        foreach (var scope in space.Scopes)
        {
            await Assert.That(Ndc(seat, scope.Centre - scope.Axes.Y * CrtCabinet.ScreenHeight / 2).Y).IsGreaterThan(face.Max(c => c.Y));
        }

        await Assert.That(Ndc(seat, space.Scopes[0].Centre).X).IsLessThan(0);
        await Assert.That(Ndc(seat, space.Scopes[1].Centre).X).IsGreaterThan(0);
    }

    [Test]
    public async Task A_landscape_screen_keeps_its_framing_and_the_scopes_over_the_corners_with_the_room_laid_out()
    {
        var (alone, seated) = (new SeatCamera(), new SeatCamera());
        alone.Update(null, 16f / 9, TimeSpan.Zero);
        seated.Update(null, 16f / 9, TimeSpan.Zero, Space);
        await Assert.That(seated.Camera.GetViewMatrix() * seated.Camera.GetProjectionMatrix()).IsEqualTo(alone.Camera.GetViewMatrix() * alone.Camera.GetProjectionMatrix());

        // A portrait room on a screen turned sideways is framed as any landscape one is, until the room is laid out again
        var portrait = new RackSpace(Layout, portrait: true);
        seated.Update(null, 16f / 9, TimeSpan.Zero, portrait);
        await Assert.That(seated.Camera.GetViewMatrix() * seated.Camera.GetProjectionMatrix()).IsEqualTo(alone.Camera.GetViewMatrix() * alone.Camera.GetProjectionMatrix());
        await Assert.That(Space.Scopes[0].Centre.X).IsLessThan(Space.Panels.ToWorld(Space.Face.Min).X);
    }

    [Test]
    [Arguments(16f / 9, RackLayout.WideOctaves)]
    [Arguments(2.1f, RackLayout.WideOctaves)]
    [Arguments(4f / 3, RackLayout.WideOctaves)]
    [Arguments(2.22f, RackLayout.NarrowOctaves)]
    [Arguments(4f / 3, RackLayout.NarrowOctaves)]
    [Arguments(9f / 19.5f, RackLayout.NarrowOctaves)]
    public async Task Turned_to_the_side_case_its_whole_face_is_in_view_and_fills_much_of_it(float aspect, int octaves)
    {
        // A portrait screen's room is laid out for it
        var space = new RackSpace(new RackLayout(Presets.Classic(), octaves), portrait: aspect < 1);
        var seat = new SeatCamera { Turned = true, Side = space.SideFrame };
        seat.Update(null, aspect, TimeSpan.FromSeconds(10), space);

        var corners = Corners(space.Layout.Side!.Value).Select(space.Side!.Value.ToWorld).ToList();
        foreach (var corner in corners)
        {
            await Assert.That(InView(Ndc(seat, corner))).IsTrue().Because($"{corner} is out of view: {Ndc(seat, corner)}");
        }

        var extent = corners.Select(p => Vector2.Abs(Ndc(seat, p))).Aggregate(Vector2.Max);
        await Assert.That(extent.X).IsGreaterThan(0.8f).Because($"the side case fills only {extent} of the view");

        // And back at the rack, the side case's middle is out of sight
        seat.Turned = false;
        seat.Update(null, aspect, TimeSpan.FromSeconds(10), space);
        var middle = space.Side.Value.ToWorld(space.Layout.Side.Value.Centre);
        await Assert.That(InView(Ndc(seat, middle)) && Vector4.Transform(new Vector4(middle, 1), seat.Camera.GetViewMatrix()).Z < 0).IsFalse();
    }

    // Facing the rack and turned to the side case, on each screen
    public static IEnumerable<Func<(float Width, float Height, bool Turned)>> Screens() =>
        new (float, float)[] { (1280, 720), (1600, 900), (915, 412), (1024, 768), (412, 915), (360, 800) }
            .SelectMany(s => new[] { false, true }.Select(turned => (Func<(float, float, bool)>)(() => (s.Item1, s.Item2, turned))));

    [Test]
    [MethodDataSource(nameof(Screens))]
    public async Task The_turn_arrow_covers_no_control_in_view(float width, float height, bool turned)
    {
        var screen = new Vector2(width, height);
        var layout = new RackLayout(Presets.Classic(), width < height ? RackLayout.NarrowOctaves : RackLayout.WideOctaves);
        var space = new RackSpace(layout, portrait: width < height);
        var seat = new SeatCamera { Turned = turned, Side = space.SideFrame };
        seat.Update(null, width / height, TimeSpan.FromSeconds(10), space);
        var view = new SeatRackView(seat, space, screen);
        var arrow = TurnArrow.Bounds(screen, turned);

        // Each control as a point and its reach, on the screen: what is under the arrow can't be pressed
        var controls = layout.Jacks.Where(j => j.Jack.Module.Value < 1000).Select(j => (Name: j.Jack.ToString(), j.Centre, Reach: j.Radius + j.Slack))
            .Concat(layout.Knobs.Select(k => (Name: k.Parameter.Name, k.Centre, Reach: k.Radius + k.Slack)))
            .Concat(layout.Buttons.Select(b => (Name: b.Action.ToString(), b.Bounds.Centre, Reach: b.Bounds.Size.Length() / 2)))
            .Concat(layout.Wheels.Select(w => (Name: w.Parameter.Name, w.Bounds.Centre, Reach: w.Bounds.Size.Length() / 2)));
        foreach (var (name, centre, reach) in controls)
        {
            // Only what is in front of the eye and on the screen can be pressed
            var world = space.SurfaceAt(centre).ToWorld(centre);
            var at = view.ToScreen(centre);
            if (Vector3.Transform(world, seat.Camera.GetViewMatrix()).Z >= 0 || at.X < 0 || at.Y < 0 || at.X > width || at.Y > height)
            {
                continue;
            }

            var edge = view.ToScreen(centre + new Vector2(reach, 0));
            var radius = Vector2.Distance(at, edge);
            var near = Vector2.Clamp(at, arrow.Min, arrow.Max);
            await Assert.That(Vector2.Distance(near, at)).IsGreaterThan(radius).Because($"{name} at {at} is under the arrow {arrow}");
        }
    }

    [Test]
    public async Task The_side_case_stands_clear_of_the_rack_and_its_keybed_on_the_seats_left()
    {
        // Its face is all nearer the seat than the keys' front edge, and left of the seat
        var keysFront = Space.Keys.ToWorld(new Vector2(0, Layout.Keys.Max.Y)).Z;
        var corners = Corners(Layout.Side!.Value).Select(Space.Side!.Value.ToWorld).ToList();

        await Assert.That(corners.Min(c => c.Z)).IsGreaterThan(keysFront + 0.1f);
        await Assert.That(corners.Max(c => c.X)).IsLessThan(-0.2f);
        await Assert.That(Space.Side!.Value.Normal.X).IsGreaterThan(0.5f).Because("it faces the seat");
    }

    [Test]
    public async Task The_head_follows_the_mouse_only_a_little_and_rests_without_it()
    {
        var seat = new SeatCamera();
        seat.Update(new Vector2(5, -5), 16f / 9, TimeSpan.FromSeconds(10));
        await Assert.That(seat.Look.X).IsEqualTo(SeatCamera.MaxYaw).Within(1e-4f);
        await Assert.That(seat.Look.Y).IsEqualTo(-SeatCamera.MaxPitch).Within(1e-4f);

        seat.Update(null, 16f / 9, TimeSpan.FromSeconds(10));
        await Assert.That(seat.Look.Length()).IsLessThan(1e-4f);
    }

    [Test]
    [Arguments(1600f, 900f)]
    [Arguments(412f, 839f)]
    public async Task Pointing_at_a_knob_jack_key_wheel_button_scope_or_the_lights_box_on_screen_hits_it(float width, float height)
    {
        var portrait = width < height;
        var space = new RackSpace(Layout, portrait);
        var seat = new SeatCamera();
        seat.Update(portrait ? null : new Vector2(0.3f, 0.2f), width / height, TimeSpan.FromSeconds(10), space);
        var view = new SeatRackView(seat, space, new Vector2(width, height));

        var key = Layout.PianoKeys.First(k => !k.IsBlack);
        var keyFront = key.Bounds.Min + new Vector2(key.Bounds.Size.X / 2, key.Bounds.Size.Y - 10);
        (Vector2 Point, RackHit Hit)[] targets =
        [
            (Layout.Knobs.First().Centre, new RackHit.Knob(Layout.Knobs.First())),
            (Layout.Jacks.Last().Centre, new RackHit.Jack(Layout.Jacks.Last())),
            (keyFront, new RackHit.PianoKey(key)),
            (Layout.Wheels.First().Bounds.Centre, new RackHit.Wheel(Layout.Wheels.First())),
            (Layout.Cheek!.Buttons[1].Bounds.Centre, new RackHit.Button(Layout.Cheek.Buttons[1])),
            (Layout.Cheek.Jacks[0].Centre, new RackHit.Jack(Layout.Cheek.Jacks[0])),
            (Layout.Scopes[0].In.Centre, new RackHit.Jack(Layout.Scopes[0].In)),
            (Layout.Scopes[1].In.Centre, new RackHit.Jack(Layout.Scopes[1].In)),
            .. portrait ? Array.Empty<(Vector2, RackHit)>() :
            [
                (Layout.Desk!.Knobs[4].Centre, new RackHit.Knob(Layout.Desk.Knobs[4])),
                (Layout.Desk.Jacks[0].Centre, new RackHit.Jack(Layout.Desk.Jacks[0])),
            ],
        ];

        foreach (var (point, hit) in targets)
        {
            await Assert.That(Layout.HitTest(view.ToDesign(view.ToScreen(point)))).IsEqualTo(hit);
        }
    }

    [Test]
    public async Task Turned_to_the_side_case_pointing_at_a_step_knob_a_gate_button_or_a_jack_on_screen_hits_it()
    {
        var seat = new SeatCamera { Turned = true, Side = Space.SideFrame };
        seat.Update(new Vector2(-0.3f, 0.2f), 16f / 9, TimeSpan.FromSeconds(10));
        var view = new SeatRackView(seat, Space, new Vector2(1600, 900));
        var sequencer = Layout.SidePanels.First(p => p.Module.Kind == ModuleKind.Sequencer);
        var quantizer = Layout.SidePanels.First(p => p.Module.Kind == ModuleKind.Quantizer);
        var step = sequencer.Knobs.First(k => k.Parameter.Name == Modules.StepPitch(5));
        var gate = sequencer.Knobs.First(k => k.Parameter.Name == Modules.StepGate(12));

        (Vector2 Point, RackHit Hit)[] targets =
        [
            (step.Centre, new RackHit.Knob(step)),
            (gate.Centre, new RackHit.Knob(gate)),
            (sequencer.Jacks[0].Centre, new RackHit.Jack(sequencer.Jacks[0])),
            (quantizer.Jacks.Last().Centre, new RackHit.Jack(quantizer.Jacks.Last())),
        ];
        foreach (var (point, hit) in targets)
        {
            await Assert.That(Layout.HitTest(view.ToDesign(view.ToScreen(point)))).IsEqualTo(hit);
        }
    }

    [Test]
    public async Task A_light_is_coloured_by_hue_and_saturation_and_scaled_by_intensity()
    {
        await Assert.That(new LightSettings(0.5f, 0.3f, 0).Color).IsEqualTo(new Vector3(0.5f));
        await Assert.That(Vector3.Distance(new LightSettings(1, 0, 1).Color, new Vector3(1, 0, 0))).IsLessThan(1e-5f);
        await Assert.That(Vector3.Distance(new LightSettings(2, 1f / 3, 1).Color, new Vector3(0, 2, 0))).IsLessThan(1e-5f);
    }
}
