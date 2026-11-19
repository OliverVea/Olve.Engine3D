using System.Numerics;
using Olve.Engine3D.Geometry.Primitives;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Samples.Synth.Space;
using Olve.Generated.Shaders;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// The room round the synth, which stays still: a desk of dark wood on black steel legs, a dark wall behind it with a tall
/// window onto a city at night, and on the desk a mug of coffee, a snake plant, a notebook with a pencil and a pair of
/// headphones. It is built once into a single mesh of its own vertex colours (one draw, no instances to update, kept out
/// of the glow), and the view through the window is a quad of its own, whose lit windows glow. The room's two lights wash
/// the wall and the desk in their colours, and the glass gives them back faintly.
/// </summary>
public sealed class RoomScenery(
    RenderingSegment segment,
    ShaderManager shaderManager,
    MeshManager meshManager,
    GroupManager groupManager,
    DrawManager drawManager)
{
    /// <summary>The desk top's height (y = 0), its front edge and depth, and its width, in metres.</summary>
    public const float DeskFront = -0.14f, DeskDepth = 0.88f, DeskWidth = 2.3f;

    private const float DeskThickness = 0.035f, LegHeight = 0.72f;

    /// <summary>The wall's face, and the window's opening in it.</summary>
    private const float WallZ = -1.02f, WallThickness = 0.1f;

    private const float WindowHalfWidth = 0.95f, WindowBottom = 0.29f, WindowTop = 1.62f, Transom = 0.88f, GlassZ = WallZ - 0.08f;

    private static readonly Vector4 Wall = Rgb(0x1E2027, 0.06f);
    private static readonly Vector4 Floor = Rgb(0x0B0C10, 0.1f);
    private static readonly Vector4 Frame = Rgb(0x2E313A, 0.4f);
    private static readonly Vector4 Sill = Rgb(0x353943, 0.3f);
    private static readonly Vector4 Wood = Rgb(0x3A2A21, 0.38f);
    private static readonly Vector4 Steel = Rgb(0x18191E, 0.5f);
    private static readonly Vector4 Ceramic = Rgb(0xE6DFCF, 0.75f);
    private static readonly Vector4 Coffee = Rgb(0x1C110A, 0.9f);
    private static readonly Vector4 Terracotta = Rgb(0x8A5A44, 0.15f);
    private static readonly Vector4 Soil = Rgb(0x17120E, 0.05f);
    private static readonly Vector4 Leaf = Rgb(0x2D5743, 0.5f);
    private static readonly Vector4 LeafPale = Rgb(0x56785A, 0.45f);
    private static readonly Vector4 Cover = Rgb(0x1B1D24, 0.25f);
    private static readonly Vector4 Pages = Rgb(0xD9D2BF, 0.05f);
    private static readonly Vector4 Elastic = Rgb(0xF2A541, 0.3f);
    private static readonly Vector4 Pencil = Rgb(0xF3C012, 0.4f);
    private static readonly Vector4 Shaving = Rgb(0xD8B98A, 0.1f);
    private static readonly Vector4 Graphite = Rgb(0x26272B, 0.6f);
    private static readonly Vector4 Plastic = Rgb(0x15161A, 0.5f);
    private static readonly Vector4 Pad = Rgb(0x2A2C32, 0.08f);
    private static readonly Vector4 BandMetal = Rgb(0x8A9099, 0.7f);

    private static readonly Vector4 WoodGrain = new(1, 0, 0, 0);

    private GroupId<Shaders.Shared.LitVertex, Shaders.Window.Instance>? _view;
    private InstanceId<Shaders.Window.Instance>? _viewInstance;
    private float _time;

    /// <summary>Builds the room for the rack's place in it and draws it in the room's pass; the window's view glows too, and the room hides it there.</summary>
    public Result Load(PassId<IColorFormat<ITextureFormat.Rgba8Unorm>, Shaders.Shared.SpacePass> pass, RoomFinish finish, RackSpace space)
    {
        var mesh = new SceneryMesh();
        BuildRoom(mesh);
        BuildDesk(mesh);
        BuildReturn(mesh, space);
        BuildThings(mesh, space);
        if (mesh.Problems is { } problems)
        {
            return problems.Prepend("Failed to build the room");
        }

        if (shaderManager.Register(Shaders.Room.Definition, segment).TryPickProblems(out problems, out var roomShader)
            || meshManager.Register(mesh.ToBuffers(), segment).TryPickProblems(out problems, out var roomMesh)
            || groupManager.Register<Shaders.Shared.RoomVertex, Shaders.Room.Instance>(roomMesh, segment).TryPickProblems(out problems, out var roomGroup)
            || groupManager.AddInstance(roomGroup, new Shaders.Room.Instance(Matrix4x4.Identity)).TryPickProblems(out problems, out _)
            || drawManager.Register(pass, roomShader, roomGroup, new IGroupInputs.None(), RenderState.Opaque, key: 0, segment).TryPickProblems(out problems, out _)
            // Black in the glow, so the wall and the window's frame hide the lights behind them there too
            || finish.AlsoGlows(roomShader, roomGroup, new IGroupInputs.None(), RenderState.Opaque, key: 0).TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to set up the room");
        }

        // The view, drawn after the room in front of it so most of it is hidden before it is shaded
        var half = new Vector2(WindowHalfWidth, (WindowTop - WindowBottom) / 2);
        var quad = new MeshBuffers<Shaders.Shared.LitVertex>
        {
            Vertices =
            [
                new(new Vector3(-half.X, -half.Y, 0), Vector3.UnitZ), new(new Vector3(half.X, -half.Y, 0), Vector3.UnitZ),
                new(new Vector3(half.X, half.Y, 0), Vector3.UnitZ), new(new Vector3(-half.X, half.Y, 0), Vector3.UnitZ),
            ],
            Indices = [0, 1, 2, 0, 2, 3],
        };
        if (shaderManager.Register(Shaders.Window.Definition, segment).TryPickProblems(out problems, out var viewShader)
            || meshManager.Register(quad, segment).TryPickProblems(out problems, out var viewMesh)
            || groupManager.Register<Shaders.Shared.LitVertex, Shaders.Window.Instance>(viewMesh, segment).TryPickProblems(out problems, out var viewGroup)
            || groupManager.AddInstance(viewGroup, ViewInstance()).TryPickProblems(out problems, out var viewInstance)
            || drawManager.Register(pass, viewShader, viewGroup, new IGroupInputs.None(), RenderState.Opaque, key: 1, segment).TryPickProblems(out problems, out _)
            || finish.AlsoGlows(viewShader, viewGroup, new IGroupInputs.None(), RenderState.Opaque, key: 1).TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to set up the window");
        }

        _view = viewGroup;
        _viewInstance = viewInstance;
        return Result.Success();
    }

    /// <summary>Moves the window's clock on, for the towers' blinking lights.</summary>
    public Result Render(float seconds)
    {
        _time = (_time + seconds) % 3600;
        return _view is { } view && _viewInstance is { } instance ? groupManager.UpdateInstance(view, instance, ViewInstance()) : Result.Success();
    }

    private Shaders.Window.Instance ViewInstance() => new(
        Matrix4x4.CreateTranslation(0, (WindowBottom + WindowTop) / 2, GlassZ), new Vector4(_time, WindowHalfWidth, 0, 0));

    // The floor, the wall round the window's opening, its frame with a mullion and a transom, and the sill
    private static void BuildRoom(SceneryMesh mesh)
    {
        mesh.Box(new Vector3(8, 0.02f, 8), At(0, -LegHeight - 0.03f, 0), Floor, bevel: 0);
        var wallZ = WallZ - WallThickness / 2;
        const float Far = 4, Low = -LegHeight - 0.02f, High = 3;
        mesh.Box(new Vector3(Far - WindowHalfWidth, High - Low, WallThickness), At(-(Far + WindowHalfWidth) / 2, (High + Low) / 2, wallZ), Wall, bevel: 0.003f);
        mesh.Box(new Vector3(Far - WindowHalfWidth, High - Low, WallThickness), At((Far + WindowHalfWidth) / 2, (High + Low) / 2, wallZ), Wall, bevel: 0.003f);
        mesh.Box(new Vector3(2 * WindowHalfWidth, WindowBottom - Low, WallThickness), At(0, (WindowBottom + Low) / 2, wallZ), Wall, bevel: 0.003f);
        mesh.Box(new Vector3(2 * WindowHalfWidth, High - WindowTop, WallThickness), At(0, (High + WindowTop) / 2, wallZ), Wall, bevel: 0.003f);

        const float Bar = 0.04f, Depth = 0.05f;
        var frameZ = GlassZ + Depth / 2 - 0.005f;
        var height = WindowTop - WindowBottom;
        var middle = (WindowTop + WindowBottom) / 2;
        mesh.Box(new Vector3(Bar, height, Depth), At(-WindowHalfWidth + Bar / 2, middle, frameZ), Frame, bevel: 0.004f);
        mesh.Box(new Vector3(Bar, height, Depth), At(WindowHalfWidth - Bar / 2, middle, frameZ), Frame, bevel: 0.004f);
        mesh.Box(new Vector3(2 * WindowHalfWidth, Bar, Depth), At(0, WindowTop - Bar / 2, frameZ), Frame, bevel: 0.004f);
        mesh.Box(new Vector3(2 * WindowHalfWidth, Bar, Depth), At(0, WindowBottom + Bar / 2, frameZ), Frame, bevel: 0.004f);
        mesh.Box(new Vector3(0.032f, height, Depth * 0.8f), At(0, middle, frameZ), Frame, bevel: 0.004f);
        mesh.Box(new Vector3(2 * WindowHalfWidth, 0.028f, Depth * 0.8f), At(0, Transom, frameZ), Frame, bevel: 0.004f);

        // The sill reaches out over the wall a little, under the desk's back edge
        mesh.Box(new Vector3(2 * WindowHalfWidth + 0.06f, 0.024f, WallZ - GlassZ + 0.05f), At(0, WindowBottom - 0.012f, (GlassZ + WallZ + 0.05f) / 2), Sill, bevel: 0.004f);
    }

    // The desk: a slab of dark wood, its front edge rounded, on four square steel legs
    private static void BuildDesk(SceneryMesh mesh)
    {
        var middle = DeskFront - DeskDepth / 2;
        mesh.Box(new Vector3(DeskWidth, DeskThickness, DeskDepth), At(0, -DeskThickness / 2, middle), Wood, bevel: 0.008f, WoodGrain, segments: 4);
        foreach (var x in new[] { -1f, 1f })
        {
            foreach (var z in new[] { DeskFront - 0.06f, DeskFront - DeskDepth + 0.06f })
            {
                mesh.Box(new Vector3(0.04f, LegHeight - DeskThickness, 0.04f), At(x * (DeskWidth / 2 - 0.06f), -(LegHeight + DeskThickness) / 2, z), Steel, bevel: 0.004f);
            }
        }
    }

    // Left of the seat, under the side case, the desk's return: a slab of the same wood joined to the desk's front edge
    // and running back past the seat, on two legs of its own; and the room's left wall behind it, to see when turned
    private static void BuildReturn(SceneryMesh mesh, RackSpace space)
    {
        if (space.Side is not { } face || space.Layout.Side is not { } area)
        {
            return;
        }

        // Out from the side case's front edge, as wide as the desk is deep and reaching from the desk to past its far end
        var near = face.ToWorld(new Vector2(area.Max.X, area.Max.Y)).X + 0.06f;
        var far = -DeskWidth / 2;
        var back = face.ToWorld(area.Min).Z + 0.12f;
        var size = new Vector3(near - far, DeskThickness, back - DeskFront);
        mesh.Box(size, At((near + far) / 2, -DeskThickness / 2, DeskFront + size.Z / 2), Wood, bevel: 0.008f, WoodGrain, segments: 4);
        foreach (var x in new[] { far + 0.06f, near - 0.06f })
        {
            mesh.Box(new Vector3(0.04f, LegHeight - DeskThickness, 0.04f), At(x, -(LegHeight + DeskThickness) / 2, back - 0.06f), Steel, bevel: 0.004f);
        }

        const float Low = -LegHeight - 0.02f, High = 3, WallX = -DeskWidth / 2 - 0.45f;
        mesh.Box(new Vector3(WallThickness, High - Low, 4), At(WallX - WallThickness / 2, (High + Low) / 2, WallZ + 2), Wall, bevel: 0.003f);
    }

    // What stands on the desk, left of the synth, clear of the keybed's cheek: the mug in front, the notebook behind it
    // and the headphones put down beside it, and the plant at the back by the wall
    private static void BuildThings(SceneryMesh mesh, RackSpace space)
    {
        var left = space.Face.Min.X * RackSpace.Scale - space.Width / 2;
        Mug(mesh, Matrix4x4.CreateRotationY(-0.6f) * Matrix4x4.CreateTranslation(left - 0.11f, 0, -0.27f));
        Plant(mesh, Matrix4x4.CreateTranslation(left - 0.2f, 0, -0.82f));
        Notebook(mesh, Matrix4x4.CreateRotationY(-0.35f) * Matrix4x4.CreateTranslation(left - 0.17f, 0, -0.5f));
        Headphones(mesh, Matrix4x4.CreateRotationY(-0.7f) * Matrix4x4.CreateTranslation(left - 0.4f, 0, -0.66f));
    }

    private static void Mug(SceneryMesh mesh, Matrix4x4 at)
    {
        // Out and up the side, over the rim and down the inside to its floor
        Vector2[] body =
        [
            new(0.034f, 0), new(0.037f, 0.003f), new(0.039f, 0.012f), new(0.039f, 0.09f), new(0.0378f, 0.0945f), new(0.0355f, 0.0945f),
            new(0.0345f, 0.09f), new(0.0345f, 0.014f), new(0.03f, 0.011f), new(0, 0.011f),
        ];
        mesh.Lathe(body, 28, at, Ceramic, LatheCaps.Start);
        mesh.Lathe([new Vector2(0.0346f, 0.074f), new Vector2(0, 0.074f)], 28, at, Coffee);
        var handle = Enumerable.Range(0, 11).Select(i =>
        {
            var a = -MathF.PI / 2 + MathF.PI * i / 10;
            return new Vector3(0.036f + 0.022f * MathF.Cos(a), 0.05f + 0.026f * MathF.Sin(a), 0);
        }).ToArray();
        mesh.Tube(handle, 0.0055f, at, Ceramic);
    }

    // A snake plant: stiff sword leaves standing up out of a terracotta pot, each leaning out a little its own way
    private static void Plant(SceneryMesh mesh, Matrix4x4 at)
    {
        Vector2[] pot =
        [
            new(0.05f, 0), new(0.055f, 0.004f), new(0.062f, 0.105f), new(0.068f, 0.108f), new(0.068f, 0.122f), new(0.063f, 0.124f),
            new(0.06f, 0.11f),
        ];
        mesh.Lathe(pot, 24, at, Terracotta, LatheCaps.Start);
        mesh.Lathe([new Vector2(0.0605f, 0.104f), new Vector2(0, 0.106f)], 24, at, Soil);
        (float Yaw, float Lean, float Height, float Width, float Out)[] leaves =
        [
            (0.2f, 0.06f, 0.42f, 0.026f, 0.01f), (1.4f, 0.16f, 0.34f, 0.024f, 0.025f), (2.5f, 0.12f, 0.38f, 0.028f, 0.02f),
            (3.6f, 0.2f, 0.3f, 0.022f, 0.03f), (4.7f, 0.1f, 0.4f, 0.025f, 0.018f), (5.6f, 0.22f, 0.27f, 0.02f, 0.035f),
            (0.9f, 0.26f, 0.24f, 0.02f, 0.04f), (3.0f, 0.04f, 0.46f, 0.027f, 0.006f), (4.1f, 0.28f, 0.22f, 0.019f, 0.04f),
        ];
        foreach (var (leaf, i) in leaves.Select((l, i) => (l, i)))
        {
            // A long thin lens, its foot in the soil
            var shape = Matrix4x4.CreateScale(leaf.Width, leaf.Height / 2, 0.005f)
                        * Matrix4x4.CreateTranslation(0, leaf.Height / 2 - 0.01f, 0)
                        * Matrix4x4.CreateRotationZ(leaf.Lean)
                        * Matrix4x4.CreateTranslation(-leaf.Out, 0.1f, 0)
                        * Matrix4x4.CreateRotationY(leaf.Yaw)
                        * at;
            mesh.Sphere(shape, i % 3 == 1 ? LeafPale : Leaf);
        }
    }

    // A closed notebook, its pages showing between the covers, an amber elastic round it and a pencil on top
    private static void Notebook(SceneryMesh mesh, Matrix4x4 at)
    {
        var size = new Vector3(0.15f, 0.003f, 0.21f);
        mesh.Box(size, Matrix4x4.CreateTranslation(0, 0.0015f, 0) * at, Cover, bevel: 0.0012f);
        mesh.Box(new Vector3(0.144f, 0.012f, 0.204f), Matrix4x4.CreateTranslation(0.002f, 0.009f, 0) * at, Pages, bevel: 0.001f);
        mesh.Box(size, Matrix4x4.CreateTranslation(0, 0.0165f, 0) * at, Cover, bevel: 0.0012f);
        mesh.Box(new Vector3(0.007f, 0.0195f, 0.2125f), Matrix4x4.CreateTranslation(0.058f, 0.0093f, 0) * at, Elastic, bevel: 0.002f);
        // Along x, its hexagonal body, the sharpened wood and the lead
        var pencil = Matrix4x4.CreateRotationZ(-MathF.PI / 2) * Matrix4x4.CreateRotationY(0.5f) * Matrix4x4.CreateTranslation(-0.07f, 0.0218f, 0.03f) * at;
        mesh.Lathe([new Vector2(0, 0), new Vector2(0.0036f, 0), new Vector2(0.0036f, 0.15f)], 6, pencil, Pencil, LatheCaps.Start);
        mesh.Lathe([new Vector2(0.0036f, 0.15f), new Vector2(0.0013f, 0.166f)], 12, pencil, Shaving);
        mesh.Lathe([new Vector2(0.0013f, 0.166f), new Vector2(0, 0.171f)], 12, pencil, Graphite);
    }

    // Headphones put down flat: both cups on their pads, the band arcing back between them
    private static void Headphones(SceneryMesh mesh, Matrix4x4 at)
    {
        const float Apart = 0.09f;
        Vector2[] pad = [new(0.026f, 0.001f), new(0.038f, 0), new(0.043f, 0.004f), new(0.043f, 0.011f), new(0.039f, 0.015f), new(0.026f, 0.015f)];
        Vector2[] shell = [new(0.04f, 0.014f), new(0.041f, 0.024f), new(0.037f, 0.031f), new(0.025f, 0.035f), new(0, 0.036f)];
        foreach (var side in new[] { -1f, 1f })
        {
            var cup = Matrix4x4.CreateTranslation(side * Apart, 0, 0) * at;
            mesh.Lathe(pad, 24, cup, Pad);
            mesh.Lathe(shell, 24, cup, Plastic);
            mesh.Tube([new Vector3(side * (Apart + 0.038f), 0.025f, 0), new Vector3(side * (Apart + 0.05f), 0.026f, -0.012f)], 0.0045f, at, BandMetal);
        }

        var band = Enumerable.Range(0, 17).Select(i =>
        {
            var a = MathF.PI * i / 16;
            return new Vector3(MathF.Cos(a) * (Apart + 0.05f), 0.026f + 0.006f * MathF.Sin(a), -0.012f - MathF.Sin(a) * 0.15f);
        }).ToArray();
        mesh.Tube(band, 0.0065f, at, Plastic);
    }

    private static Matrix4x4 At(float x, float y, float z) => Matrix4x4.CreateTranslation(x, y, z);

    private static Vector4 Rgb(int rgb, float shine) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, shine);
}

/// <summary>Shapes from the engine's primitives, placed and coloured, merged into one mesh of the room's vertices.</summary>
public sealed class SceneryMesh
{
    private readonly List<Shaders.Shared.RoomVertex> _vertices = [];
    private readonly List<uint> _indices = [];

    /// <summary>The first shape that failed, if one did.</summary>
    public ResultProblemCollection? Problems { get; private set; }

    public void Box(Vector3 size, Matrix4x4 at, Vector4 albedo, float bevel, Vector4 finish = default, int segments = 2) =>
        Add(bevel > 0 ? LitMeshes.Box(size, bevel, segments) : LitMeshes.Box(size, 0.0005f, 2), at, albedo, finish);

    public void Lathe(Vector2[] profile, int segments, Matrix4x4 at, Vector4 albedo, LatheCaps caps = LatheCaps.None) =>
        Add(LitMeshes.Lathe(profile, segments, caps), at, albedo, default);

    /// <summary>A unit sphere, shaped by the matrix.</summary>
    public void Sphere(Matrix4x4 at, Vector4 albedo)
    {
        var profile = Enumerable.Range(0, 9).Select(k => new Vector2(MathF.Sin(MathF.PI * k / 8), -MathF.Cos(MathF.PI * k / 8))).ToArray();
        profile[0] = new Vector2(0, -1);
        profile[^1] = new Vector2(0, 1);
        Add(LitMeshes.Lathe(profile, 10), at, albedo, default);
    }

    public void Tube(Vector3[] path, float radius, Matrix4x4 at, Vector4 albedo)
    {
        if (TubeShape.Create(path.Length, radius, 10, capped: true).TryPickProblems(out var problems, out var shape))
        {
            Problems ??= problems;
            return;
        }

        var vertices = new Shaders.Shared.LitVertex[shape.VertexCount];
        var indices = new uint[shape.IndexCount];
        Geometry.Primitives.Tube.Populate<Shaders.Shared.LitVertex>(shape, path, vertices);
        Geometry.Primitives.Tube.GetIndices(shape, indices);
        Add(new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] }, at, albedo, default);
    }

    private void Add(Result<MeshBuffers<Shaders.Shared.LitVertex>> shape, Matrix4x4 at, Vector4 albedo, Vector4 finish)
    {
        if (shape.TryPickProblems(out var problems, out var buffers))
        {
            Problems ??= problems;
            return;
        }

        // Normals by the inverse transpose, as a shape may be scaled unevenly
        Matrix4x4.Invert(at, out var inverse);
        var normals = Matrix4x4.Transpose(inverse);
        var offset = (uint)_vertices.Count;
        foreach (var v in buffers.Vertices)
        {
            _vertices.Add(new Shaders.Shared.RoomVertex(Vector3.Transform(v.position, at), Vector3.Normalize(Vector3.TransformNormal(v.normal, normals)), albedo, finish));
        }

        foreach (var index in buffers.Indices ?? [.. Enumerable.Range(0, buffers.Vertices.Length).Select(i => (uint)i)])
        {
            _indices.Add(index + offset);
        }
    }

    public MeshBuffers<Shaders.Shared.RoomVertex> ToBuffers() => new() { Vertices = [.. _vertices], Indices = [.. _indices] };
}
