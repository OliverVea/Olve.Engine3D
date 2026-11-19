using System.Numerics;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Generated.Shaders;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// Panel ink: flat shapes painted on surfaces in the room, in each surface's design units (x right, y down), immediate
/// mode like <see cref="ShapeCanvas"/>. <see cref="On"/> picks the surface the next shapes go on; paint is lit like its
/// surface, and <see cref="Glowing"/> shapes give their own light.
/// </summary>
public sealed class InkCanvas(InstanceBatch<IVertexData.None, Shaders.Ink.Instance> batch)
{
    private const float Rect = 0, Disc = 1, Arc = 2, Line = 3, Glyph = 4, Shade = 5;

    private Matrix4x4 _surface = Matrix4x4.Identity;
    private float _glow;

    public static readonly Shaders.Ink.Instance Hidden = new(default, Vector4.Zero, Vector4.Zero, Vector4.Zero, Vector4.Zero, Vector4.Zero);

    public void Begin() => batch.Begin();

    public Result End() => batch.End();

    /// <summary>Puts the next shapes on a surface: its design units to world, as <see cref="Space.Surface.ToMatrix"/> gives.</summary>
    public InkCanvas On(Matrix4x4 surface)
    {
        _surface = surface;
        _glow = 0;
        return this;
    }

    /// <summary>The next shapes glow, from 0 (paint) to 1; until the next <see cref="On"/> or call.</summary>
    public InkCanvas Glowing(float glow = 1)
    {
        _glow = glow;
        return this;
    }

    public void Box(Vector2 min, Vector2 max, Vector4 color, float radius = 0, float outline = 0) =>
        Add(min, max, new Vector4(Rect, radius, outline, 0), Vector4.Zero, color);

    public void Circle(Vector2 centre, float radius, Vector4 color, float innerRadius = 0) =>
        Add(centre - new Vector2(radius), centre + new Vector2(radius), new Vector4(Disc, innerRadius, 0, 0), Vector4.Zero, color);

    /// <summary>A domed lens: a disc whose surface bulges by <paramref name="dome"/> (0 flat, 1 a half sphere), glossy even when it glows.</summary>
    public void Lens(Vector2 centre, float radius, Vector4 color, float dome = 0.8f) =>
        Add(centre - new Vector2(radius), centre + new Vector2(radius), new Vector4(Disc, 0, 0, 0), new Vector4(0, 0, dome, 0), color);

    /// <summary>
    /// A soft shadow round the foot of a part standing on the surface: as dark as <paramref name="strength"/> out to
    /// <paramref name="solid"/> of the radius, then fading to nothing at it.
    /// </summary>
    public void Shadow(Vector2 centre, float radius, float strength, float solid = 0.45f) =>
        Add(centre - new Vector2(radius), centre + new Vector2(radius), new Vector4(Shade, solid, 0, 0), Vector4.Zero, new Vector4(0, 0, 0, strength));

    /// <summary>Part of a ring, clockwise from angle <paramref name="from"/> to <paramref name="to"/> (radians, 0 straight up).</summary>
    public void ArcRing(Vector2 centre, float radius, float innerRadius, float from, float to, Vector4 color) =>
        Add(centre - new Vector2(radius), centre + new Vector2(radius), new Vector4(Arc, innerRadius, 0, 0), new Vector4(from, to, 0, 0), color);

    /// <summary>A line with round ends; with a <paramref name="dash"/> above 0, dashed, a dash and a gap every <paramref name="dash"/> along it.</summary>
    public void Segment(Vector2 from, Vector2 to, float width, Vector4 color, float dash = 0)
    {
        var reach = new Vector2(width / 2);
        Add(Vector2.Min(from, to) - reach, Vector2.Max(from, to) + reach, new Vector4(Line, width, dash, 0), new Vector4(from, to.X, to.Y), color);
    }

    /// <summary>A line of dot-matrix text centred on a point, each dot <paramref name="dot"/> design units.</summary>
    public void TextCentred(Vector2 centre, string text, float dot, Vector4 color)
    {
        var topLeft = centre - new Vector2(DotFont.Width(text), DotFont.Rows) * dot / 2;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ')
            {
                continue;
            }

            var (low, high) = DotFont.Glyph(text[i]);
            var min = topLeft + new Vector2(i * DotFont.Advance * dot, 0);
            Add(min, min + new Vector2(DotFont.Columns, DotFont.Rows) * dot, new Vector4(Glyph, low, high, 0), Vector4.Zero, color);
        }
    }

    /// <summary>A row of <paramref name="count"/> character cells with every dot on, centred on a point: a display's unlit dots.</summary>
    public void Cells(Vector2 centre, int count, float dot, Vector4 color)
    {
        var width = (count - 1) * DotFont.Advance + DotFont.Columns;
        var topLeft = centre - new Vector2(width, DotFont.Rows) * dot / 2;
        for (var i = 0; i < count; i++)
        {
            var min = topLeft + new Vector2(i * DotFont.Advance * dot, 0);
            Add(min, min + new Vector2(DotFont.Columns, DotFont.Rows) * dot, new Vector4(Glyph, (1 << 20) - 1, (1 << 15) - 1, 0), Vector4.Zero, color);
        }
    }

    private void Add(Vector2 min, Vector2 max, Vector4 shape, Vector4 extra, Vector4 color) =>
        batch.Add(new Shaders.Ink.Instance(_surface, new Vector4(min, max.X, max.Y), shape, extra, color, new Vector4(_glow, 0, 0, 0)));
}
