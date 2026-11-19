using System.Numerics;

namespace Olve.Engine3D.Samples.Synth.Rendering;

/// <summary>Where a cable's plugs sit, in metres, and which way each comes out of its jack.</summary>
public readonly record struct CableEnds(Vector3 From, Vector3 FromOut, Vector3 To, Vector3 ToOut);

/// <summary>How a cable hangs apart from its neighbours: how far its middle is pushed aside, and how much more it sags.</summary>
public readonly record struct CableBow(Vector3 Aside, float Slack)
{
    public static readonly CableBow Straight = new(Vector3.Zero, 1);
}

/// <summary>
/// The cables' paths: out of each plug along its jack's normal, then hanging between them, sagging more the longer it is.
/// Cables that share a jack, or run between the same two places, would lie on top of each other, so they fan out: the
/// shortest hangs as it would alone, and each longer one is pushed a lane further aside, toward the rack's nearer side
/// (or down, for one running across), and hangs a little slacker.
/// </summary>
public static class CableRoutes
{
    /// <summary>How many points along a cable its tube follows.</summary>
    public const int Samples = 40;

    /// <summary>How far apart neighbouring cables' bows are, in metres, at their control points; their middles about three quarters of it.</summary>
    public const float Lane = 0.035f;

    /// <summary>How much more each lane out sags than the one before.</summary>
    public const float LaneSlack = 0.12f;

    // Plugs closer than this are on the same jack; ends closer than this run between the same places, in metres
    private const float SameJack = 0.004f, SamePlace = 0.03f;

    /// <summary>Each cable's bow, in order: the same cables always fan out the same way.</summary>
    public static CableBow[] Fan(IReadOnlyList<CableEnds> cables)
    {
        var bows = Enumerable.Repeat(CableBow.Straight, cables.Count).ToArray();
        var group = Enumerable.Range(0, cables.Count).ToArray();
        int Find(int i) => group[i] == i ? i : group[i] = Find(group[i]);
        for (var i = 0; i < cables.Count; i++)
        {
            for (var j = i + 1; j < cables.Count; j++)
            {
                if (Neighbours(cables[i], cables[j]))
                {
                    group[Find(j)] = Find(i);
                }
            }
        }

        foreach (var members in Enumerable.Range(0, cables.Count).GroupBy(Find).Where(g => g.Count() > 1))
        {
            var middle = members.Aggregate(Vector3.Zero, (sum, i) => sum + (cables[i].From + cables[i].To) / 2) / members.Count();
            // Shortest first, then in patch order, so a fan's cables nest rather than cross
            var lanes = members.OrderBy(i => Vector3.Distance(cables[i].From, cables[i].To)).ThenBy(i => i).ToList();
            for (var lane = 1; lane < lanes.Count; lane++)
            {
                var i = lanes[lane];
                bows[i] = new CableBow(Aside(cables[i], middle) * Lane * lane, 1 + LaneSlack * lane);
            }
        }

        return bows;
    }

    /// <summary>The points a cable's tube follows, from its first plug to its second.</summary>
    public static Vector3[] Path(CableEnds cable, CableBow bow)
    {
        var start = cable.From + cable.FromOut * 0.022f;
        var end = cable.To + cable.ToOut * 0.022f;
        var sag = Vector3.UnitY * (0.025f + 0.28f * Vector3.Distance(start, end)) * bow.Slack;
        var a = start + cable.FromOut * 0.04f - sag + bow.Aside;
        var b = end + cable.ToOut * 0.04f - sag + bow.Aside;
        var path = new Vector3[Samples];
        for (var i = 0; i < Samples; i++)
        {
            var t = i / (float)(Samples - 1);
            var u = 1 - t;
            path[i] = u * u * u * start + 3 * u * u * t * a + 3 * u * t * t * b + t * t * t * end;
        }

        return path;
    }

    private static bool Neighbours(CableEnds a, CableEnds b) =>
        Near(a.From, b.From, SameJack) || Near(a.To, b.To, SameJack) || Near(a.From, b.To, SameJack) || Near(a.To, b.From, SameJack)
        || (Near(a.From, b.From, SamePlace) && Near(a.To, b.To, SamePlace))
        || (Near(a.From, b.To, SamePlace) && Near(a.To, b.From, SamePlace));

    private static bool Near(Vector3 a, Vector3 b, float within) => Vector3.Distance(a, b) < within;

    // Across the cable, in the surface its plugs come out of: outward from the rack's middle, or down for one running across
    private static Vector3 Aside(CableEnds cable, Vector3 middle)
    {
        var across = Vector3.Cross(cable.To - cable.From, cable.FromOut + cable.ToOut);
        if (across.LengthSquared() < 1e-10f)
        {
            across = Vector3.UnitX;
        }

        across = Vector3.Normalize(across);
        var outward = MathF.Abs(across.X) >= 0.3f ? MathF.Sign(across.X) * MathF.Sign(middle.X == 0 ? 1 : middle.X) : -MathF.Sign(across.Y);
        return across * (outward == 0 ? 1 : outward);
    }
}
