using System.Numerics;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Generated.Shaders;
using Olve.Utilities.IntegerMath2D;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// The scopes' CRT screens. Each scope draws its trace into a texture of its own that keeps what it held: first a dark veil
/// that fades the old trace like phosphor, then a wide faint stroke under a thin bright one. The room then draws the
/// screen's glass showing that texture, and a soft halo in front of it as bright as the trace is tall.
/// </summary>
public sealed class ScopeScreens(
    RenderingSegment segment,
    PassManager passManager,
    ShaderManager shaderManager,
    MeshManager meshManager,
    GroupManager groupManager,
    DrawManager drawManager,
    TextureManager textureManager,
    SamplerManager samplerManager)
{
    private const int TextureWidth = 512, TextureHeight = 384;

    /// <summary>The phosphor's colour: a P31 green.</summary>
    public static readonly Vector4 Phosphor = new(0.45f, 1f, 0.55f, 1);

    // How much of the last frame's trace the veil hides each frame: enough that an Rgba8 target fades all the way
    private const float Veil = 0.22f;

    // The strokes: half their width in texture pixels, and how bright
    private const float WideHalfWidth = 7, WideBrightness = 0.16f;
    private const float ThinHalfWidth = 1.8f, ThinBrightness = 0.95f;

    // How much of the screen the trace spans, so it stays clear of the curved glass's edge
    private const float Span = 0.92f;

    private const int VeilVertices = 6;
    private static readonly int StrokeVertices = 2 * (ScopeService.Points - 1) * 6;

    private readonly List<Screen> _screens = [];

    private sealed record Screen(
        MeshId<Shaders.Trace.Vertex> Trace,
        Shaders.Trace.Vertex[] Vertices,
        InstanceBatch<Shaders.Shared.LitVertex, Shaders.Crt.Instance> Glass,
        InstanceBatch<Shaders.Shared.LitVertex, Shaders.Crt.Instance> Halo);

    /// <summary>Sets up a screen for each scope, drawn in the room's pass with the glass and halo meshes given.</summary>
    public Result Load(PassId<IColorFormat<ITextureFormat.Rgba8Unorm>, Shaders.Shared.SpacePass> room, RoomFinish finish, MeshBuffers<Shaders.Shared.LitVertex> glass, float glassWidth, float glassHeight, int count)
    {
        if (shaderManager.Register(Shaders.Trace.Definition, segment).TryPickProblems(out var problems, out var traceShader)
            || shaderManager.Register(Shaders.Crt.Definition, segment).TryPickProblems(out problems, out var crtShader)
            || samplerManager.Register<ISamplerKind.Filtering>(new SamplerDescription(TextureFilter.Linear, TextureWrap.ClampToEdge), segment)
                .TryPickProblems(out problems, out var sampler)
            || meshManager.Register(glass, segment).TryPickProblems(out problems, out var glassMesh)
            || meshManager.Register(HaloQuad(glassWidth * 1.9f, glassHeight * 2.1f), segment).TryPickProblems(out problems, out var haloMesh))
        {
            return problems;
        }

        for (var i = 0; i < count; i++)
        {
            var vertices = new Shaders.Trace.Vertex[VeilVertices + StrokeVertices];
            if (textureManager.Create<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>(new Size(TextureWidth, TextureHeight), segment)
                    .TryPickProblems(out problems, out var texture)
                || passManager.Register(ColorAttachment.Keep(texture), new IPassInputs.None(), priority: -10 + i, segment).TryPickProblems(out problems, out var pass)
                || meshManager.Register(new MeshBuffers<Shaders.Trace.Vertex> { Vertices = [.. vertices] }, segment).TryPickProblems(out problems, out var traceMesh)
                || groupManager.Register<Shaders.Trace.Vertex, Shaders.Trace.Instance>(traceMesh, segment).TryPickProblems(out problems, out var traceGroup)
                || groupManager.AddInstance(traceGroup, new Shaders.Trace.Instance(Phosphor)).TryPickProblems(out problems, out _)
                || drawManager.Register(pass, traceShader, traceGroup, new IGroupInputs.None(), new RenderState(BlendMode.Alpha, DepthWrite: false, DepthTest: false), key: 0, segment)
                    .TryPickProblems(out problems, out _))
            {
                return problems.Prepend("Failed to set up a scope's trace");
            }

            var inputs = new Shaders.Crt.Inputs { UTrace = TextureSampler.Of(texture, sampler) };
            if (groupManager.Register<Shaders.Shared.LitVertex, Shaders.Crt.Instance>(glassMesh, segment).TryPickProblems(out problems, out var glassGroup)
                || drawManager.Register(room, crtShader, glassGroup, inputs, RenderState.Opaque, key: 0, segment).TryPickProblems(out problems, out _)
                || groupManager.Register<Shaders.Shared.LitVertex, Shaders.Crt.Instance>(haloMesh, segment).TryPickProblems(out problems, out var haloGroup)
                || drawManager.Register(room, crtShader, haloGroup, inputs, new RenderState(BlendMode.Alpha, DepthWrite: false), key: 1, segment).TryPickProblems(out problems, out _)
                || finish.AlsoGlows(crtShader, glassGroup, inputs, RenderState.Opaque, key: 0).TryPickProblems(out problems)
                || finish.AlsoGlows(crtShader, haloGroup, inputs, new RenderState(BlendMode.Alpha, DepthWrite: false), key: 1).TryPickProblems(out problems))
            {
                return problems.Prepend("Failed to set up a scope's screen");
            }

            var hidden = new Shaders.Crt.Instance(default, Vector4.Zero, Vector4.Zero);
            _screens.Add(new Screen(traceMesh, vertices,
                new InstanceBatch<Shaders.Shared.LitVertex, Shaders.Crt.Instance>(groupManager, glassGroup, hidden),
                new InstanceBatch<Shaders.Shared.LitVertex, Shaders.Crt.Instance>(groupManager, haloGroup, hidden)));
        }

        return Result.Success();
    }

    /// <summary>Draws a scope's trace into its texture, and its glass and halo at the screen's centre in the room.</summary>
    /// <param name="screenFrame">The glass's frame: x right, y up, z out of the screen, its origin at the glass's back.</param>
    public Result Draw(int index, ScopeScreen screen, Matrix4x4 screenFrame, float glassWidth, float glassHeight)
    {
        var target = _screens[index];
        FillTrace(screen, target.Vertices);
        if (meshManager.Update(target.Trace, new MeshBuffers<Shaders.Trace.Vertex> { Vertices = [.. target.Vertices] }).TryPickProblems(out var problems))
        {
            return problems;
        }

        var halo = screen.Live ? 0.25f + 0.75f * screen.Peak : 0;
        target.Glass.Begin();
        target.Glass.Add(new Shaders.Crt.Instance(screenFrame, new Vector4(glassWidth, glassHeight, 0, 0), Phosphor));
        target.Halo.Begin();
        if (halo > 0)
        {
            // Just in front of the bezel, so it glows over its inner edge
            var front = Matrix4x4.CreateTranslation(0, 0, Space.CrtCabinet.BezelDepth + 0.002f) * screenFrame;
            target.Halo.Add(new Shaders.Crt.Instance(front, new Vector4(glassWidth, glassHeight, 1, halo), Phosphor));
        }

        return Result.Concat(() => target.Glass.End(), () => target.Halo.End());
    }

    // The veil over the whole texture, then each stroke as quads along the trace, the wide one first
    private static void FillTrace(ScopeScreen screen, Shaders.Trace.Vertex[] vertices)
    {
        Quad(vertices, 0, new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(2, Veil), new Vector2(2, Veil));
        if (!screen.Live)
        {
            Array.Clear(vertices, VeilVertices, StrokeVertices);
            return;
        }

        var points = screen.Points;
        var at = VeilVertices;
        foreach (var (halfWidth, brightness) in new[] { (WideHalfWidth, WideBrightness), (ThinHalfWidth, ThinBrightness) })
        {
            for (var i = 0; i < points.Length - 1; i++)
            {
                var a = ToPixels(i, points[i], points.Length);
                var b = ToPixels(i + 1, points[i + 1], points.Length);
                var along = b - a;
                var direction = along.LengthSquared() > 1e-6f ? Vector2.Normalize(along) : Vector2.UnitX;
                var across = new Vector2(-direction.Y, direction.X) * halfWidth;
                // Each quad reaches a little past its ends, so the joins between segments have no gaps
                var start = a - direction * halfWidth * 0.5f;
                var end = b + direction * halfWidth * 0.5f;
                Quad(vertices, at, ToClip(start - across), ToClip(end - across), ToClip(start + across), ToClip(end + across),
                    new Vector2(-1, brightness), new Vector2(1, brightness));
                at += 6;
            }
        }
    }

    // A point of the trace in texture pixels, y up
    private static Vector2 ToPixels(int index, float value, int count) => new(
        (0.5f + (index / (float)(count - 1) - 0.5f) * Span) * TextureWidth,
        (0.5f + value * 0.5f * Span) * TextureHeight);

    private static Vector2 ToClip(Vector2 pixels) => new(pixels.X / TextureWidth * 2 - 1, pixels.Y / TextureHeight * 2 - 1);

    // Two triangles: the first two corners along one edge, the next two along the other
    private static void Quad(Shaders.Trace.Vertex[] vertices, int at, Vector2 a0, Vector2 b0, Vector2 a1, Vector2 b1, Vector2 edge0, Vector2 edge1)
    {
        vertices[at] = new(a0, edge0);
        vertices[at + 1] = new(b0, edge0);
        vertices[at + 2] = new(a1, edge1);
        vertices[at + 3] = new(a1, edge1);
        vertices[at + 4] = new(b0, edge0);
        vertices[at + 5] = new(b1, edge1);
    }

    // A flat quad facing out of the screen, centred on it
    private static MeshBuffers<Shaders.Shared.LitVertex> HaloQuad(float width, float height)
    {
        Vector3[] corners = [new(-1, -1, 0), new(1, -1, 0), new(-1, 1, 0), new(-1, 1, 0), new(1, -1, 0), new(1, 1, 0)];
        return new MeshBuffers<Shaders.Shared.LitVertex>
        {
            Vertices = [.. corners.Select(c => new Shaders.Shared.LitVertex(c * new Vector3(width / 2, height / 2, 0), Vector3.UnitZ))],
        };
    }
}
