using System.Numerics;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Generated.Shaders;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// Draws 2D shapes in pixels, immediate mode: each frame the caller draws everything again, back to front, and the canvas
/// updates only the instances that changed. All shapes are one instanced draw, so they layer in the order drawn.
/// </summary>
public sealed class ShapeCanvas(
    RenderingSegment segment,
    PassManager passManager,
    ShaderManager shaderManager,
    MeshManager meshManager,
    GroupManager groupManager,
    DrawManager drawManager)
{
    private const float Rect = 0, Disc = 1, Arc = 2, Line = 3, Glyph = 4;

    private static readonly Shaders.Shape.Instance Hidden = new(Vector4.Zero, Vector4.Zero, Vector4.Zero, Vector4.Zero);

    private readonly List<Shaders.Shape.Instance> _frame = [];
    private readonly List<(InstanceId<Shaders.Shape.Instance> Id, Shaders.Shape.Instance Data)> _slots = [];
    private PassId<IColorFormat<ITextureFormat.Rgba8Unorm>, Shaders.Shared.ShapePass> _pass = null!;
    private ShapeGroupId _group = null!;

    /// <summary>Sets up the canvas's pass at a priority, clearing the screen first or drawing over what is there.</summary>
    public Result Load(int priority, ITextureFormat.Rgba8Unorm? clear)
    {
        Load<ITextureFormat.Rgba8Unorm> load = clear is { } background
            ? new Load<ITextureFormat.Rgba8Unorm>.Clear(background)
            : new Load<ITextureFormat.Rgba8Unorm>.Keep();
        var screen = ColorAttachment.Of(TextureManager.Screen, load);
        if (passManager.Register(screen, new Shaders.Shared.ShapePass { UResolution = Vector2.One }, priority, segment)
            .TryPickProblems(out var problems, out var pass))
        {
            return problems.Prepend("Failed to create the screen pass");
        }

        _pass = pass;
        if (shaderManager.Register(Shaders.Shape.Definition, segment).TryPickProblems(out problems, out var shader))
        {
            return problems.Prepend("Failed to register the shape shader");
        }

        // Six vertices without data: the shader builds the quad's two triangles from the vertex index
        var quad = new MeshBuffers<IVertexData.None> { Vertices = [default, default, default, default, default, default] };
        if (meshManager.Register(quad, segment).TryPickProblems(out problems, out var mesh)
            || groupManager.Register<IVertexData.None, Shaders.Shape.Instance>(mesh, segment).TryPickProblems(out problems, out var group))
        {
            return problems.Prepend("Failed to create the shape group");
        }

        _group = group;
        return drawManager.Register(_pass, shader, _group, new IGroupInputs.None(), RenderState.AlphaBlendNoDepth, key: 0, segment)
            .TryPickProblems(out problems, out _)
            ? problems.Prepend("Failed to create the shape draw")
            : Result.Success();
    }

    public void Begin() => _frame.Clear();

    /// <summary>Uploads the frame's shapes, drawn on a screen of this size.</summary>
    public Result End(Vector2 screen)
    {
        if (passManager.SetInputs(_pass, new Shaders.Shared.ShapePass { UResolution = screen }).TryPickProblems(out var problems))
        {
            return problems;
        }

        for (var i = 0; i < int.Max(_frame.Count, _slots.Count); i++)
        {
            var data = i < _frame.Count ? _frame[i] : Hidden;
            if (i >= _slots.Count)
            {
                if (groupManager.AddInstance(_group, data).TryPickProblems(out problems, out var id))
                {
                    return problems;
                }

                _slots.Add((id, data));
            }
            else if (_slots[i].Data != data)
            {
                if (groupManager.UpdateInstance(_group, _slots[i].Id, data).TryPickProblems(out problems))
                {
                    return problems;
                }

                _slots[i] = (_slots[i].Id, data);
            }
        }

        return Result.Success();
    }

    /// <summary>A box with rounded corners, filled, or only its outline when <paramref name="outline"/> is above 0.</summary>
    public void Box(Vector2 min, Vector2 max, Vector4 color, float radius = 0, float outline = 0) =>
        Add(min, max, new Vector4(Rect, radius, outline, 0), Vector4.Zero, color);

    /// <summary>A disc, or a ring when <paramref name="innerRadius"/> is above 0.</summary>
    public void Circle(Vector2 centre, float radius, Vector4 color, float innerRadius = 0) =>
        Add(centre - new Vector2(radius), centre + new Vector2(radius), new Vector4(Disc, innerRadius, 0, 0), Vector4.Zero, color);

    /// <summary>Part of a ring, clockwise from angle <paramref name="from"/> to <paramref name="to"/> (radians, 0 straight up).</summary>
    public void ArcRing(Vector2 centre, float radius, float innerRadius, float from, float to, Vector4 color) =>
        Add(centre - new Vector2(radius), centre + new Vector2(radius), new Vector4(Arc, innerRadius, 0, 0), new Vector4(from, to, 0, 0), color);

    /// <summary>A segment with round ends.</summary>
    public void Segment(Vector2 from, Vector2 to, float width, Vector4 color)
    {
        var reach = new Vector2(width / 2);
        Add(Vector2.Min(from, to) - reach, Vector2.Max(from, to) + reach, new Vector4(Line, width, 0, 0), new Vector4(from, to.X, to.Y), color);
    }

    /// <summary>A line of dot-matrix text, its top left at <paramref name="topLeft"/>, each dot <paramref name="dot"/> pixels.</summary>
    public void Text(Vector2 topLeft, string text, float dot, Vector4 color)
    {
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

    /// <summary>A line of text centred on a point.</summary>
    public void TextCentred(Vector2 centre, string text, float dot, Vector4 color) =>
        Text(centre - new Vector2(DotFont.Width(text), DotFont.Rows) * dot / 2, text, dot, color);

    private void Add(Vector2 min, Vector2 max, Vector4 shape, Vector4 extra, Vector4 color) =>
        _frame.Add(new Shaders.Shape.Instance(new Vector4(min, max.X, max.Y), shape, extra, color));
}
