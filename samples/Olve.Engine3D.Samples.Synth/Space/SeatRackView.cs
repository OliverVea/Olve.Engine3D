using System.Numerics;
using Olve.Engine3D.Camera;
using Olve.Engine3D.Math;
using Olve.Engine3D.Samples.Synth.Rack;

namespace Olve.Engine3D.Samples.Synth.Space;

/// <summary>
/// The rack seen from the seat: a point on the screen is the point of the panels or keybed its ray meets, so the layout's
/// hit test works as it does on a flat rack. A point on the turn arrow is on none of it.
/// </summary>
public sealed class SeatRackView(SeatCamera seat, RackSpace space, Vector2 screen) : IRackView
{
    /// <summary>The on-screen arrow that turns the seat, in pixels: the pointers there press it, not the rack.</summary>
    public Box? Arrow { get; init; }

    public Vector2 ToScreen(Vector2 design)
    {
        var world = space.SurfaceAt(design).ToWorld(design);
        var clip = Vector4.Transform(new Vector4(world, 1), seat.Camera.GetViewMatrix() * seat.Camera.GetProjectionMatrix());
        var ndc = new Vector2(clip.X, clip.Y) / clip.W;
        return new Vector2((ndc.X + 1) / 2 * screen.X, (1 - ndc.Y) / 2 * screen.Y);
    }

    /// <summary>
    /// The point of the layout under a screen point: of the nearest part the ray meets, the panels, the keys, the cheek or
    /// the LIGHTS box; off all of them, the panels' plane, so a drag carries on past their edge.
    /// </summary>
    public Vector2 ToDesign(Vector2 screenPoint)
    {
        if (Arrow is { } arrow && arrow.Contains(screenPoint))
        {
            return new Vector2(float.NaN);
        }

        var ndc = new Vector2(screenPoint.X / screen.X * 2 - 1, 1 - screenPoint.Y / screen.Y * 2);
        if (seat.Camera.GetRay(ndc).TryPickProblems(out _, out var ray))
        {
            return new Vector2(float.NaN);
        }

        if (space.ScopeJackOnRay(ray.Origin, ray.Direction) is { } scope)
        {
            return scope.In.Centre;
        }

        Vector2? nearest = null;
        var nearestAlong = float.PositiveInfinity;
        foreach (var region in space.Regions)
        {
            if (ray.TryIntersectPlane(region.Surface.Plane, out var point)
                && region.Surface.ToDesign(point) is var design && region.Area.Contains(design)
                && Vector3.Dot(point - ray.Origin, ray.Direction) is var along && along > 0 && along < nearestAlong)
            {
                nearest = design;
                nearestAlong = along;
            }
        }

        if (nearest is { } hit)
        {
            return hit;
        }

        return ray.TryIntersectPlane(space.Panels.Plane, out var onPanels) ? space.Panels.ToDesign(onPanels) : new Vector2(float.NaN);
    }
}
