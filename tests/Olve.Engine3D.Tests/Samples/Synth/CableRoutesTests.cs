using System.Numerics;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Samples.Synth.Rendering;
using Olve.Engine3D.Samples.Synth.Space;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class CableRoutesTests
{
    // On a 1280x720 screen, or a phone held upright
    [Test]
    [Arguments(1280f, 720f)]
    [Arguments(412f, 839f)]
    public async Task Each_of_the_default_patchs_cables_can_be_told_apart_from_the_seat(float width, float height)
    {
        var patch = Presets.Classic();
        var layout = new RackLayout(patch, width < height ? RackLayout.NarrowOctaves : RackLayout.WideOctaves);
        var space = new RackSpace(layout, portrait: width < height);
        var seat = new SeatCamera();
        seat.Update(null, width / height, TimeSpan.Zero, space);
        var jacks = layout.Jacks.ToDictionary(j => j.Jack, j => j.Centre);

        // The plugs as the room draws them: on each jack, coming out of its surface
        CableEnds Ends(Cable cable)
        {
            var (from, to) = (space.SurfaceAt(jacks[cable.From]), space.SurfaceAt(jacks[cable.To]));
            return new CableEnds(from.ToWorld(jacks[cable.From]) + from.Normal * 0.008f, from.Normal, to.ToWorld(jacks[cable.To]) + to.Normal * 0.008f, to.Normal);
        }

        // The rack's cables: the side case's are behind the seat's turn
        var ends = patch.Cables.Where(c => !RackLayout.InSideCase(patch.Find(c.From.Module)!.Kind) && !RackLayout.InSideCase(patch.Find(c.To.Module)!.Kind))
            .Select(Ends).ToList();
        var bows = CableRoutes.Fan(ends);
        var matrix = seat.Camera.GetViewMatrix() * seat.Camera.GetProjectionMatrix();
        var screen = new Vector2(width, height);
        Vector2 OnScreen(Vector3 p)
        {
            var clip = Vector4.Transform(new Vector4(p, 1), matrix);
            return (new Vector2(clip.X, -clip.Y) / clip.W + Vector2.One) / 2 * screen;
        }

        var paths = ends.Select((e, i) => CableRoutes.Path(e, bows[i]).Select(OnScreen).ToArray()).ToList();

        // Away from its plugs, most of each cable is clear of every other by more than a thick tube's width on screen, as
        // wide as 9 mm across the panels' middle
        await Assert.That(paths.Count).IsEqualTo(18);
        var middleOfPanels = space.Panels.ToWorld(new Vector2(layout.CaseWidth / 2, space.PanelsHeight / 2));
        var clear = Vector2.Distance(OnScreen(middleOfPanels), OnScreen(middleOfPanels + Vector3.UnitX * 0.009f));
        for (var a = 0; a < paths.Count; a++)
        {
            for (var b = 0; b < paths.Count; b++)
            {
                if (a == b)
                {
                    continue;
                }

                var middle = paths[a][(CableRoutes.Samples / 5)..(CableRoutes.Samples * 4 / 5)];
                var covered = middle.Count(p => Distance(p, paths[b]) < clear) / (float)middle.Length;
                await Assert.That(covered).IsLessThan(0.4f).Because($"{patch.Cables[a]} lies along {patch.Cables[b]}");
            }
        }
    }

    [Test]
    public async Task Cables_from_one_jack_fan_out_in_lanes_and_a_cable_alone_hangs_straight()
    {
        var from = new Vector3(-0.3f, 0.1f, -0.3f);
        CableEnds To(float height) => new(from, Vector3.UnitY, new Vector3(-0.3f, height, -0.5f), Vector3.UnitZ);
        CableEnds[] cables = [To(0.5f), To(0.3f), new(new Vector3(0.2f, 0.2f, -0.5f), Vector3.UnitZ, new Vector3(0.3f, 0.3f, -0.5f), Vector3.UnitZ), To(0.4f)];

        var bows = CableRoutes.Fan(cables);

        // The shortest hangs as it would alone; each longer one a lane further out from the rack's middle, and slacker
        await Assert.That(bows[1]).IsEqualTo(CableBow.Straight);
        await Assert.That(bows[2]).IsEqualTo(CableBow.Straight);
        await Assert.That(bows[3].Aside.X).IsEqualTo(-CableRoutes.Lane).Within(1e-4f);
        await Assert.That(bows[0].Aside.X).IsEqualTo(-2 * CableRoutes.Lane).Within(1e-4f);
        await Assert.That(bows[0].Slack).IsGreaterThan(bows[3].Slack);
        await Assert.That(bows[3].Slack).IsGreaterThan(1);
    }

    private static float Distance(Vector2 point, Vector2[] path)
    {
        var nearest = float.PositiveInfinity;
        for (var i = 1; i < path.Length; i++)
        {
            var (a, b) = (path[i - 1], path[i]);
            var t = float.Clamp(Vector2.Dot(point - a, b - a) / MathF.Max(1e-6f, (b - a).LengthSquared()), 0, 1);
            nearest = MathF.Min(nearest, Vector2.Distance(point, a + (b - a) * t));
        }

        return nearest;
    }
}
