using System.Numerics;
using Olve.Engine3D.Geometry.Primitives;
using Olve.Engine3D.Input;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Samples.Synth.Space;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Time;
using Olve.Generated.Shaders;
using Olve.Utilities.Ids;
using Olve.Utilities.IntegerMath2D;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// Draws the synth in the room each frame, seen from the seat: the case with its rim and name plate, the panels sunk in it
/// and screwed down, knobs, jacks and cables, the keys between two cheeks, the two CRT scopes and the lights by them, with
/// the panels' text and graphics in panel ink. The hint line stays flat on
/// the screen, over the room.
/// </summary>
public class SpaceRenderService(
    RenderingSegment segment,
    PassManager passManager,
    ShaderManager shaderManager,
    MeshManager meshManager,
    GroupManager groupManager,
    DrawManager drawManager,
    ShapeCanvas overlay,
    RackInputService rackInput,
    SeatService seat,
    RackLightsService lights,
    ScopeService scopes,
    ScopeScreens scopeScreens,
    OutputLevelsService levels,
    RoomFinish finish,
    RoomScenery scenery,
    SynthRack rack,
    IWindowInfo window,
    DeltaTimeService time,
    RenderingManager renderingManager) : ISceneService
{
    private static readonly ITextureFormat.Rgba8Unorm Background = new(20, 21, 27, 255);

    private static readonly Vector4 Panel = Rgb(0x2B2E37);
    private static readonly Vector4 PanelEdge = Rgb(0x3D414C);
    private static readonly Vector4 Plate = Rgb(0x1C1E25);
    private static readonly Vector4 Ink = Rgb(0xECE6D6);
    private static readonly Vector4 Soft = Rgb(0x9DA3AE);
    private static readonly Vector4 Muted = Rgb(0x5D626D);
    private static readonly Vector4 Accent = Rgb(0xF2A541);
    private static readonly Vector4 Warning = Rgb(0xF0735A);
    private static readonly Vector4 KnobBody = Rgb(0x1A1C22);
    private static readonly Vector4 KnobTrack = Rgb(0x3A3E48);
    private static readonly Vector4 JackMetal = Rgb(0xA4AAB4);
    private static readonly Vector4 JackHole = Rgb(0x0A0B0E);
    private static readonly Vector4 WhiteKey = Rgb(0xE9E5DA);
    private static readonly Vector4 BlackKey = Rgb(0x23252C);
    private static readonly Vector4 Case = Rgb(0x23252D);
    private static readonly Vector4 Screen = Rgb(0x0D1311);
    private static readonly Vector4 Rubber = Rgb(0xE8E0CB);
    private static readonly Vector4 SegmentGlass = Rgb(0x070B08);
    private static readonly Vector4 SegmentLit = new(0.42f, 1f, 0.55f, 1);
    private static readonly Vector4 SegmentGhost = Rgb(0x0F2416);
    private static readonly Vector4 ButtonCap = Rgb(0x3A3D46);

    // Finishes (see lit.slang's iFinish): brushed anodised plates, bare metal, and parts that darken toward their foot
    private static readonly Vector4 BrushedPlate = new(1, 0.25f, 0, 0);
    private static readonly Vector4 BrushedRail = new(1, 0.5f, 0, 0);
    private static readonly Vector4 BareMetal = new(0, 1, 0, 0);
    private static readonly Vector4 CapInsert = new(1, 0.9f, 0, 1);
    private static readonly Vector4 KnobFinish = new(0, 0.12f, 0.007f, 0);
    private static readonly Vector4 JackFinish = new(0, 1, 0.0012f, 0);
    private static readonly Vector4 CableSheen = new(0, 0.3f, 0, 0);
    private static readonly Vector4 Aluminium = Rgb(0xB9BEC6);
    private static readonly Vector4 Felt = Rgb(0x111216);
    private static readonly Vector4 MeterOff = Rgb(0x3A2C18);
    private static readonly Vector3 FaderCapSize = new(0.016f, 0.008f, 0.01f);

    /// <summary>Each group's stripe: sources amber, modulation blue, shaping teal, effects purple, output pink, utilities grey.</summary>
    private static readonly Vector4[] GroupColors = [Rgb(0xD08A3C), Rgb(0x5B8FD9), Rgb(0x4FB3A3), Rgb(0xA77FE0), Rgb(0xD06A8A), Rgb(0x9DA3AE)];

    /// <summary>How far in from a card's left its heading starts.</summary>
    private const float PanelTitleInset = 12;
    private static readonly Vector4 RailMetal = Rgb(0x575C66);

    /// <summary>The cables' colours, by what they carry (<see cref="SignalKind"/>): amber for audio, green for control voltages, white for gates.</summary>
    private static readonly Vector4[] CableColors = [Rgb(0xF2A541), Rgb(0x8FE0A8), Rgb(0xF2F2F2)];


    // Soft light from the room: cool from above, dimmer and warmer from the floor
    private static readonly Vector3 SkyAmbient = new(0.075f, 0.08f, 0.095f);
    private static readonly Vector3 GroundAmbient = new(0.03f, 0.027f, 0.026f);

    /// <summary>The key light's colour and strength: a warm white that the side lights tint.</summary>
    private static readonly Vector3 KeyColor = new Vector3(1, 0.9f, 0.78f) * 2.1f;

    /// <summary>How bright the side lights are at intensity 1, as they fade with distance.</summary>
    private const float LightStrength = 1.5f;

    /// <summary>The case's moulded plastic grain, from 0 to 1.</summary>
    private const float Grain = 1;

    /// <summary>The synth's name, on the plate between the panels and the keys.</summary>
    private const string Name = "OLVE MODULAR";

    // A knob turns from 135 degrees left of straight up to 135 degrees right
    private const float KnobSweep = 0.75f * MathF.PI;

    private const float TitleDot = 2.4f;
    private const float LabelDot = 1.25f;
    private const float CardTitleDot = 2;

    // Heights off the panel face, in metres: the panels stand on the case, ink on the panels
    private const float PanelThickness = 0.004f;
    private const float InkLift = PanelThickness + 0.0004f;

    // The dark gap between neighbouring plates, in metres
    private const float PanelGap = 0.0014f;

    // How far the rim stands off the face: proud of the panels, which sit in it
    private const float RimHeight = 0.011f;

    private const float CableRadius = 0.0024f;

    // How much thicker a cable carrying the keys' voices is, and its plugs
    private const float PolyGirth = 1.8f, PolyPlug = 1.3f;

    // The speakers: the opening in each panel, how deep the cone sits behind it, and the grille's weave, in metres
    private float SpeakerRadius => rackInput.Layout.Speakers[0].Size.X * RackSpace.Scale * 0.38f;
    private const float SpeakerRecess = 0.03f;
    private const float WirePitch = 0.0032f, WireShare = 0.38f;
    private static readonly Vector4 GrillePaint = Rgb(0x15161A) with { W = 0.55f };
    private static readonly Vector4 ConePaper = Rgb(0x1E1F23);
    private static readonly Vector4 DarkMetal = Rgb(0x2A2C32);

    // An output's border and its ring of level lights, out from the jack's edge, in design units: the border's gap, the
    // lights' distance and size, and how far round they reach either way of straight up (135 degrees)
    private const float LevelBorder = 1.6f, LevelRing = 5.6f, LevelLight = 1.1f;
    private const float LevelArc = 0.75f * MathF.PI;
    private static readonly Vector4 LevelOn = Rgb(0xF4F1EA);
    private static readonly Vector4 LevelOff = Rgb(0x2A2C31);

    // The keybed's thumb wheels, in metres, and how far they roll either way of their middle, in radians
    private const float WheelRadius = 0.034f, WheelWidth = 0.024f, WheelRoll = 0.6f;
    private const int TubeSides = 16;

    // The scopes' glass, a little inside the bezel's opening
    private const float GlassWidth = CrtCabinet.ScreenWidth - 0.002f, GlassHeight = CrtCabinet.ScreenHeight - 0.002f;

    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private readonly Dictionary<(Vector3 Size, bool Occludes), Batch> _boxes = [];
    private readonly List<InstanceBatch<Shaders.Shared.LitVertex, Shaders.Lit.Instance>> _batches = [];
    private readonly Dictionary<Vector2, InstanceBatch<Shaders.Shared.LitVertex, Shaders.Lit.Instance>> _panelBoxes = [];

    // What stays put is drawn once, into the still layer of each batch; the cables' plugs into theirs when a cable moves
    private const int StillLayer = 0, CableLayer = 1;
    private readonly BatchLayers _layers = new();
    private bool _drawnStill;
    private JackLayout?[] _scopeJacks = [];

    // The layout everything derived from it was worked out for (FollowLayout)
    private RackLayout? _layout;

    // This frame's: the output's level, its meter's reading, the cables being dragged and the notes sounding
    private float _outputLevel;
    private float _outputReading = OutputMeter.Floor - 1;
    private readonly List<RackPointer> _cableDrags = [];
    private readonly HashSet<int> _sounding = [];

    // What each knob drew last, kept while what it shows stays the same, and each quantizer's readout
    private readonly Dictionary<KnobLayout, Memo<(float, bool)>> _knobMemos = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<PanelLayout, Memo<int>> _readoutMemos = new(ReferenceEqualityComparer.Instance);

    // How loud the output is, smoothed, for the room's finish
    private float _loudness;

    private PassId<IColorFormat<ITextureFormat.Rgba8Unorm>, Shaders.Shared.SpacePass> _pass = null!;
    private ShaderId<Shaders.Shared.LitVertex, Shaders.Lit.Instance, IColorFormat<ITextureFormat.Rgba8Unorm>, Shaders.Shared.SpacePass, IGroupInputs.None> _litShader = null!;
    private RackSpace _space = null!;
    private MeshId<Shaders.Shared.LitVertex>? _postsMesh;
    private InkCanvas _ink = null!;
    private Batch _plinth = null!, _keybed = null!, _knob = null!, _pointer = null!, _nut = null!, _sleeve = null!,
        _hole = null!, _whiteKey = null!, _blackKey = null!, _lamp = null!, _plug = null!, _posts = null!, _screw = null!, _caseScrew = null!,
        _wheel = null!, _insert = null!, _boot = null!, _bead = null!, _faderCap = null!, _faderRib = null!, _toggleCap = null!;

    // A push button's cap stands this far off its panel
    private const float ToggleHeight = 0.0045f;
    // The cables' tubes, a mesh per colour: the patch's, kept in the cable layer, and the ones being dragged, drawn each frame
    private readonly Dictionary<int, (MeshId<Shaders.Shared.LitVertex> Mesh, Batch Batch)> _cables = [], _draggedCables = [];
    private readonly List<CableShape> _cableShapes = [], _drawnCables = [], _draggedShapes = [];

    // Where a plug sits on each jack, and which way it comes out
    private readonly Dictionary<JackRef, (Vector3 Top, Vector3 Out)> _jackTops = [];

    // Voices is how many the cable carries, 1 for one signal; the shape holds it so a change of VOICES draws the beads again
    // and its bow apart from the cables it would lie on (CableRoutes.Fan)
    private readonly record struct CableShape(CableEnds Ends, int Color, int Voices, CableBow Bow)
    {
        public bool Poly => Voices > 1;
    }
    private InstanceBatch<Shaders.Shared.LitVertex, Shaders.Grille.Instance> _grilles = null!;
    private Batch _speakerPlate = null!, _speakerWall = null!, _cone = null!, _speakerRim = null!;


    private sealed class Batch(InstanceBatch<Shaders.Shared.LitVertex, Shaders.Lit.Instance> batch)
    {
        public void Add(Matrix4x4 world, Vector4 albedo, float shine = 0.3f, Vector4 emissive = default, float grain = 0, Vector4 finish = default) =>
            batch.Add(new Shaders.Lit.Instance(world, albedo with { W = shine }, emissive with { W = grain }, finish));
    }

    public int Priority => SceneServicePriority.FromDependencies([rackInput, seat, lights]);

    public Result Load()
    {
        if (FollowLayout().TryPickProblems(out var layoutProblems))
        {
            return layoutProblems;
        }

        return Result.Concat(
                () => LoadPass(),
                () => LoadMeshes(),
                () => overlay.Load(priority: 100, clear: null))
            .IfProblem(p => p.Prepend("Failed to set up the room"));
    }

    // Works out again what is derived from the rack's layout, and lets go of what was drawn from it, when the layout is
    // a new one: where things sit in the room, the jacks' plugs, the still layer, the knobs' memos and the
    // cables. A panel of a new size gets a plate of its own (PanelMeshes); the meshes made at load for the keys and the
    // speakers keep their sizes. The room drawn is the seat's, which pointers reach the rack through; a portrait screen
    // has a room of its own, so turning the screen lays it out again too, moving the scopes and their stands.
    private Result FollowLayout()
    {
        var layout = rackInput.Layout;
        var space = seat.Space is { } seated && seated.Layout == layout ? seated : null;
        if (ReferenceEquals(layout, _layout) && (space is null || ReferenceEquals(space, _space)))
        {
            return Result.Success();
        }

        _layout = layout;
        _space = space ?? new RackSpace(layout);
        _jackTops.Clear();
        foreach (var jack in layout.Jacks)
        {
            _jackTops[jack.Jack] = JackTop(jack.Centre);
        }

        _scopeJacks = [.. _space.Scopes.Select((_, i) => layout.Scopes.FirstOrDefault(s => s.Index == i)?.In)];
        _knobMemos.Clear();
        _drawnStill = false;
        _drawnCables.Clear();
        // The scopes' stands are made at load, and again when the scopes move
        if (_postsMesh is not { } posts)
        {
            return Result.Success();
        }

        return Posts().TryPickProblems(out var problems, out var buffers) ? problems : meshManager.Update(posts, buffers);
    }

    private Result LoadPass()
    {
        // The room is drawn into the finish's scene, which the finish lays on the screen
        if (finish.Load().TryPickProblems(out var problems)
            || passManager.Register(ColorAttachment.Of(finish.Scene, new Load<ITextureFormat.Rgba8Unorm>.Clear(Background)),
                DepthAttachment.Clear(finish.SceneDepth, 1f), new Shaders.Shared.SpacePass(), priority: 0, segment)
            .TryPickProblems(out problems, out var pass))
        {
            return problems;
        }

        if (shaderManager.Register(Shaders.Lit.Definition, segment).TryPickProblems(out problems, out var litShader)
            || shaderManager.Register(Shaders.Ink.Definition, segment).TryPickProblems(out problems, out var inkShader))
        {
            return problems;
        }

        _pass = pass;
        _litShader = litShader;

        // Ink is drawn after the solid meshes, tested against them but not written, so overlapping shapes layer in order
        var quad = new MeshBuffers<IVertexData.None> { Vertices = [default, default, default, default, default, default] };
        if (meshManager.Register(quad, segment).TryPickProblems(out problems, out var quadMesh)
            || groupManager.Register<IVertexData.None, Shaders.Ink.Instance>(quadMesh, segment).TryPickProblems(out problems, out var inkGroup)
            || drawManager.Register(_pass, inkShader, inkGroup, new IGroupInputs.None(), new RenderState(BlendMode.Alpha, DepthWrite: false), key: 0, segment)
                .TryPickProblems(out problems, out _)
            || finish.AlsoGlows(inkShader, inkGroup, new IGroupInputs.None(), new RenderState(BlendMode.Alpha, DepthWrite: false), key: 0).TryPickProblems(out problems))
        {
            return problems;
        }

        _ink = new InkCanvas(_layers.Add(new InstanceBatch<IVertexData.None, Shaders.Ink.Instance>(groupManager, inkGroup, InkCanvas.Hidden)));
        return LoadGrille();
    }

    // The speakers' grilles: a dome each, woven in its shader and drawn over the cones behind it, which show through
    private Result LoadGrille()
    {
        if (shaderManager.Register(Shaders.Grille.Definition, segment).TryPickProblems(out var problems, out var grilleShader)
            || meshManager.Register(Dome(SpeakerRadius - 0.0006f, 0.009f), segment).TryPickProblems(out problems, out var domeMesh)
            || groupManager.Register<Shaders.Shared.LitVertex, Shaders.Grille.Instance>(domeMesh, segment).TryPickProblems(out problems, out var domeGroup)
            || drawManager.Register(_pass, grilleShader, domeGroup, new IGroupInputs.None(), new RenderState(BlendMode.Alpha, DepthWrite: false), key: 2, segment)
                .TryPickProblems(out problems, out _)
            || scenery.Load(_pass, finish, _space).TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to set up the speakers' grilles and the room");
        }

        _grilles = _layers.Add(new InstanceBatch<Shaders.Shared.LitVertex, Shaders.Grille.Instance>(groupManager, domeGroup, new Shaders.Grille.Instance(default, Vector4.Zero, Vector4.Zero)));
        return Result.Success();
    }

    private Result LoadMeshes()
    {
        var keyDepth = RackLayout.WhiteKeyHeight * RackSpace.Scale * RackSpace.KeyDepth;
        var whiteWidth = rackInput.Layout.PianoKeys.First(k => !k.IsBlack).Bounds.Size.X;
        var blackWidth = rackInput.Layout.PianoKeys.FirstOrDefault(k => k.IsBlack)?.Bounds.Size.X ?? whiteWidth * RackLayout.BlackKeyShare;
        var blackDepth = RackLayout.BlackKeyHeight * RackSpace.Scale * RackSpace.KeyDepth;
        var knob = RackLayout.KnobRadius * RackSpace.Scale;
        // A knurled skirt, then the fluted body rounding over into its domed top, where the metal insert sits
        (Vector2, float, int)[] knobRings =
        [
            (new(knob * 1.12f, 0), 0, 0), (new(knob * 1.12f, 0.0006f), 0.0003f, 16), (new(knob * 1.12f, 0.0028f), 0.0003f, 16),
            (new(knob * 1.06f, 0.0037f), 0, 0), (new(knob * 0.86f, 0.0043f), 0, 0), (new(knob * 0.86f, 0.0052f), 0.0011f, 16),
            (new(knob * 0.81f, 0.0148f), 0.0011f, 16), (new(knob * 0.79f, 0.0167f), 0, 0), (new(knob * 0.73f, 0.0183f), 0, 0),
            (new(knob * 0.64f, 0.0192f), 0, 0), (new(knob * 0.3f, 0.0197f), 0, 0), (new(0, 0.0198f), 0, 0),
        ];
        Vector2[] insertProfile = [new(knob * 0.58f, 0.0186f), new(knob * 0.58f, 0.0199f), new(knob * 0.555f, 0.0202f), new(0, 0.0202f)];
        // A hex nut, its top edge chamfered bright
        Vector2[] nutProfile = [new(0.0085f, 0), new(0.0085f, 0.0018f), new(0.0076f, 0.0028f), new(0.0062f, 0.003f), new(0, 0.003f)];
        Vector2[] sleeveProfile = [new(0.0056f, 0.003f), new(0.0056f, 0.0066f), new(0.0036f, 0.0066f)];
        Vector2[] holeProfile = [new(0.0036f, 0.0066f), new(0.0036f, 0.0012f), new(0, 0.0012f)];
        // A plug: a knurled metal barrel, then a rubber boot tapering onto the cable
        (Vector2, float, int)[] barrelRings =
        [
            (new(0.0044f, 0), 0, 0), (new(0.0048f, 0.0006f), 0, 0), (new(0.0048f, 0.0014f), 0.0003f, 8), (new(0.0048f, 0.0114f), 0.0003f, 8),
            (new(0.0047f, 0.0124f), 0, 0), (new(0.004f, 0.0129f), 0, 0), (new(0.0025f, 0.013f), 0, 0),
        ];
        Vector2[] bootProfile =
        [
            new(0.0041f, 0.0122f), new(0.0042f, 0.0158f), new(0.0037f, 0.0188f), new(CableRadius * 1.25f, 0.0222f), new(CableRadius, 0.025f),
        ];
        Vector2[] screwProfile = [new(1, 0), new(1, 0.25f), new(0.9f, 0.5f), new(0.65f, 0.72f), new(0.3f, 0.8f), new(0, 0.82f)];
        var sphere = Enumerable.Range(0, 13).Select(k => k is 0 or 12
            ? new Vector2(0, k == 0 ? -1 : 1) * 0.016f
            : new Vector2(MathF.Sin(MathF.PI * k / 12), -MathF.Cos(MathF.PI * k / 12)) * 0.016f).ToArray();

        return Result.Concat(
            () => Mesh(LitMeshes.Box(new Vector3(CaseWidth, 0.16f, 0.2f), 0.01f), b => _plinth = b),
            () => Mesh(LitMeshes.Box(new Vector3(CaseWidth, 0.04f, keyDepth + 0.05f), 0.008f), b => _keybed = b),
            () => Mesh(LitMeshes.FlutedLathe(knobRings, 48), b => _knob = b),
            () => Mesh(LitMeshes.Lathe(insertProfile, 24), b => _insert = b),
            () => Mesh(LitMeshes.Box(Vector3.One, 0.5f, 2), b => _pointer = b),
            () => Mesh(LitMeshes.Box(FaderCapSize, 0.0018f), b => _faderCap = b),
            () => Mesh(LitMeshes.Box(new Vector3(FaderCapSize.X - 0.002f, 0.0012f, 0.0012f), 0.0005f), b => _faderRib = b),
            () => Mesh(LitMeshes.Box(new Vector3(RackLayout.ToggleSize.X * RackSpace.Scale, ToggleHeight, RackLayout.ToggleSize.Y * RackSpace.Scale), 0.0012f), b => _toggleCap = b),
            () => Mesh(LitMeshes.Lathe(nutProfile, 6, LatheCaps.Start), b => _nut = b),
            () => Mesh(LitMeshes.Lathe(sleeveProfile, 32), b => _sleeve = b),
            () => Mesh(LitMeshes.Lathe(holeProfile, 32), b => _hole = b),
            () => Mesh(LitMeshes.FlutedLathe(barrelRings, 24), b => _plug = b, glows: true),
            () => Mesh(LitMeshes.Lathe(bootProfile, 24), b => _boot = b, glows: true),
            () => Mesh(LitMeshes.Lathe(sphere.Select(p => p * 0.3f).ToArray(), 16), b => _bead = b, glows: true),
            () => Mesh(LitMeshes.Lathe(screwProfile.Select(p => p * 0.0021f).ToArray(), 24, LatheCaps.Start), b => _screw = b),
            () => Mesh(LitMeshes.Lathe(screwProfile.Select(p => p * 0.0034f).ToArray(), 24, LatheCaps.Start), b => _caseScrew = b),
            () => Mesh(LitMeshes.Box(new Vector3(whiteWidth * RackSpace.Scale - 0.0008f, 0.022f, keyDepth), 0.0015f), b => _whiteKey = b, glows: true),
            () => Mesh(LitMeshes.Box(new Vector3(blackWidth * RackSpace.Scale - 0.0008f, 0.02f, blackDepth), 0.002f), b => _blackKey = b, glows: true),
            () => Mesh(LitMeshes.RibbedWheel(WheelRadius, WheelWidth, ribs: 30, ribDepth: 0.0019f, rounding: 0.0035f), b => _wheel = b),
            () => Glass(GlassWidth, GlassHeight, 0.011f).TryPickProblems(out var problems, out var glass)
                ? problems
                : scopeScreens.Load(_pass, finish, glass, GlassWidth, GlassHeight, RackLayout.MaxScopes),
            () => Mesh(LitMeshes.Lathe(sphere, 24), b => _lamp = b, glows: true),
            () => RegisterMesh(Posts()).TryPickProblems(out var problems, out var posts) ? problems : KeepPosts(posts),
            () => SpeakerMeshes(),
            () => PanelMeshes());
    }

    private Result KeepPosts((MeshId<Shaders.Shared.LitVertex> Mesh, InstanceBatch<Shaders.Shared.LitVertex, Shaders.Lit.Instance> Batch) posts)
    {
        (_postsMesh, _posts) = (posts.Mesh, new Batch(posts.Batch));
        return Result.Success();
    }

    private Result SpeakerMeshes()
    {
        var size = rackInput.Layout.Speakers[0].Size * RackSpace.Scale;
        var r = SpeakerRadius;
        // The cone: its rubber surround rolling down from the opening, the paper cone, and the dust cap in the middle
        Vector2[] cone =
        [
            new(r, -0.004f), new(r * 0.985f, -0.0028f), new(r * 0.95f, -0.0026f), new(r * 0.915f, -0.0045f), new(r * 0.88f, -0.0065f),
            new(r * 0.32f, -0.025f), new(r * 0.3f, -0.0245f), new(r * 0.24f, -0.0215f), new(r * 0.14f, -0.0195f), new(0, -0.019f),
        ];
        Vector2[] wall = [new(r, PanelThickness), new(r, -SpeakerRecess + 0.002f)];
        Vector2[] rim =
        [
            new(r + 0.007f, PanelThickness), new(r + 0.0065f, PanelThickness + 0.0016f), new(r + 0.004f, PanelThickness + 0.0026f),
            new(r + 0.0012f, PanelThickness + 0.002f), new(r, PanelThickness + 0.0005f), new(r, PanelThickness - 0.001f),
        ];
        return Result.Concat(
            () => Mesh(LitMeshes.PlateWithHole(size.X - 0.0006f, size.Y, r, PanelThickness), b => _speakerPlate = b),
            () => Mesh(LitMeshes.Lathe(wall, 64), b => _speakerWall = b),
            () => Mesh(LitMeshes.Lathe(cone, 64), b => _cone = b),
            () => Mesh(LitMeshes.Lathe(rim, 64), b => _speakerRim = b));
    }

    // A spherical cap of the base radius, rising to the height in its middle, along y
    private static MeshBuffers<Shaders.Shared.LitVertex> Dome(float radius, float height)
    {
        const int Rings = 16, Segments = 96;
        var sphere = (radius * radius + height * height) / (2 * height);
        var widest = MathF.Asin(radius / sphere);
        var vertices = new List<Shaders.Shared.LitVertex>();
        var indices = new List<uint>();
        for (var ring = 0; ring <= Rings; ring++)
        {
            var phi = widest * (1 - ring / (float)Rings);
            for (var segment = 0; segment <= Segments; segment++)
            {
                var theta = 2 * MathF.PI * segment / Segments;
                var normal = new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta));
                vertices.Add(new Shaders.Shared.LitVertex(normal * sphere + new Vector3(0, height - sphere, 0), normal));
            }
        }

        for (var ring = 0; ring < Rings; ring++)
        {
            for (var segment = 0; segment < Segments; segment++)
            {
                var at = (uint)(ring * (Segments + 1) + segment);
                var next = at + Segments + 1;
                indices.AddRange([at, next, at + 1, at + 1, next, next + 1]);
            }
        }

        return new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] };
    }

    /// <summary>The case's width in metres: the face with its rim.</summary>
    private float CaseWidth => _space.Face.Size.X * RackSpace.Scale;

    private Result PanelMeshes()
    {
        foreach (var size in rackInput.Layout.Panels.Select(p => p.Bounds.Size).Distinct().Where(size => !_panelBoxes.ContainsKey(size)))
        {
            var box = LitMeshes.Box(new Vector3(size.X * RackSpace.Scale - PanelGap, size.Y * RackSpace.Scale - PanelGap, PanelThickness), 0.0011f, 2);
            if (Register(box).TryPickProblems(out var problems, out var batch))
            {
                return problems.Prepend("Failed to create a panel");
            }

            _panelBoxes[size] = batch;
        }

        return Result.Success();
    }

    /// <summary>
    /// A box of a size, its mesh made the first time it is asked for; one that <paramref name="occludes"/> stands in front of
    /// the window's lights, so it is drawn into the glow too, to hide them there.
    /// </summary>
    private Result<Batch> BoxOf(Vector3 size, float bevel, bool occludes = false)
    {
        if (_boxes.TryGetValue((size, occludes), out var batch))
        {
            return batch;
        }

        if (Register(LitMeshes.Box(size, MathF.Min(bevel, MathF.Min(size.X, MathF.Min(size.Y, size.Z)) / 2)), glows: occludes)
            .TryPickProblems(out var problems, out var registered))
        {
            return problems;
        }

        return _boxes[(size, occludes)] = new Batch(registered);
    }

    // A CRT's glass: a rectangle bulging out toward its centre, as a piece of a sphere
    private static Result<MeshBuffers<Shaders.Shared.LitVertex>> Glass(float width, float height, float bulge)
    {
        const int Columns = 24, Rows = 18;
        var corner = new Vector2(width, height).Length() / 2;
        var radius = (corner * corner + bulge * bulge) / (2 * bulge);
        var vertices = new List<Shaders.Shared.LitVertex>();
        var indices = new List<uint>();
        for (var row = 0; row <= Rows; row++)
        {
            for (var column = 0; column <= Columns; column++)
            {
                var x = (column / (float)Columns - 0.5f) * width;
                var y = (row / (float)Rows - 0.5f) * height;
                var sphereCentre = new Vector3(0, 0, bulge - radius);
                var z = sphereCentre.Z + MathF.Sqrt(radius * radius - x * x - y * y);
                var position = new Vector3(x, y, z);
                vertices.Add(new Shaders.Shared.LitVertex(position, Vector3.Normalize(position - sphereCentre)));
            }
        }

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var at = (uint)(row * (Columns + 1) + column);
                indices.AddRange([at, at + 1, at + Columns + 1, at + 1, at + Columns + 2, at + Columns + 1]);
            }
        }

        return new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] };
    }

    // The scopes' stands from the case's top corners, and the lamps' arms from the scope cabinets
    private Result<MeshBuffers<Shaders.Shared.LitVertex>> Posts()
    {
        var paths = new List<(Vector3[] Path, float Radius)>();
        foreach (var (scope, i) in _space.Scopes.Select((s, i) => (s, i)))
        {
            var (x, y, z) = scope.Axes;
            var bottom = scope.Centre - y * (CrtCabinet.Drop + CrtCabinet.Height / 2 - 0.01f) - z * 0.12f;
            var face = _space.Face;
            // Over the case's top corners, or straight down onto its top edge where the scopes stand over it
            var standX = _space.Portrait ? (bottom.X + _space.Width / 2) / RackSpace.Scale : i == 0 ? face.Min.X + 40 : face.Max.X - 40;
            var corner = _space.Panels.ToWorld(new Vector2(standX, face.Min.Y + 10)) - _space.Panels.Normal * 0.05f;
            paths.Add(([corner, bottom], 0.011f));
            // The lamp's arm leaves the cabinet's outer side and curls up and out to the lamp
            var side = scope.Centre + x * ((i == 0 ? -1 : 1) * (CrtCabinet.Width / 2 - 0.01f)) - y * CrtCabinet.Drop - z * 0.12f;
            var lamp = _space.Lights[i];
            paths.Add(([side, side + (lamp - side) * new Vector3(0.6f, 0.1f, 0.3f), lamp], 0.004f));
        }

        return Tubes(paths, capped: true);
    }

    private static Result<MeshBuffers<Shaders.Shared.LitVertex>> Tubes(IEnumerable<(Vector3[] Path, float Radius)> paths, bool capped)
    {
        var vertices = new List<Shaders.Shared.LitVertex>();
        var indices = new List<uint>();
        foreach (var (path, radius) in paths)
        {
            if (TubeShape.Create(path.Length, radius, TubeSides, capped).TryPickProblems(out var problems, out var shape))
            {
                return problems;
            }

            var tubeVertices = new Shaders.Shared.LitVertex[shape.VertexCount];
            var tubeIndices = new uint[shape.IndexCount];
            Tube.Populate<Shaders.Shared.LitVertex>(shape, path, tubeVertices);
            Tube.GetIndices(shape, tubeIndices);
            var offset = (uint)vertices.Count;
            vertices.AddRange(tubeVertices);
            indices.AddRange(tubeIndices.Select(index => index + offset));
        }

        // A mesh needs a triangle, even with nothing to draw
        if (vertices.Count == 0)
        {
            vertices.AddRange([default, default, default]);
            indices.AddRange([0, 1, 2]);
        }

        return new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] };
    }

    private Result Mesh(Result<MeshBuffers<Shaders.Shared.LitVertex>> buffers, Action<Batch> keep, bool glows = false)
    {
        if (Register(buffers, glows).TryPickProblems(out var problems, out var batch))
        {
            return problems;
        }

        keep(new Batch(batch));
        return Result.Success();
    }

    private Result<InstanceBatch<Shaders.Shared.LitVertex, Shaders.Lit.Instance>> Register(Result<MeshBuffers<Shaders.Shared.LitVertex>> buffers, bool glows = false) =>
        RegisterMesh(buffers, glows).TryPickProblems(out var problems, out var registered) ? problems : registered.Batch;

    // Only parts that can glow, the cables that hang in front of them, and the case and cabinets in front of the window's
    // lights, are drawn into the glow too: the rest would only draw black there, over nothing that glows
    private Result<(MeshId<Shaders.Shared.LitVertex> Mesh, InstanceBatch<Shaders.Shared.LitVertex, Shaders.Lit.Instance> Batch)> RegisterMesh(
        Result<MeshBuffers<Shaders.Shared.LitVertex>> buffers, bool glows = false)
    {
        if (buffers.TryPickProblems(out var problems, out var data)
            || meshManager.Register(data, segment).TryPickProblems(out problems, out var mesh)
            || groupManager.Register<Shaders.Shared.LitVertex, Shaders.Lit.Instance>(mesh, segment).TryPickProblems(out problems, out var group)
            || drawManager.Register(_pass, _litShader, group, new IGroupInputs.None(), RenderState.Opaque, key: 0, segment).TryPickProblems(out problems, out _)
            || glows && finish.AlsoGlows(_litShader, group, new IGroupInputs.None(), RenderState.Opaque, key: 0).TryPickProblems(out problems))
        {
            return problems;
        }

        var batch = _layers.Add(new InstanceBatch<Shaders.Shared.LitVertex, Shaders.Lit.Instance>(groupManager, group, new Shaders.Lit.Instance(default, Vector4.Zero, Vector4.Zero, Vector4.Zero)));
        _batches.Add(batch);
        return (mesh, batch);
    }

    public Result Render()
    {
        var newLayout = !ReferenceEquals(rackInput.Layout, _layout);
        if (FollowLayout().TryPickProblems(out var layoutProblems) || (newLayout && PanelMeshes().TryPickProblems(out layoutProblems)))
        {
            return layoutProblems;
        }

        var camera = seat.Seat.Camera;
        var room = new Shaders.Shared.SpacePass
        {
            View = camera.GetViewMatrix(),
            Projection = camera.GetProjectionMatrix(),
            CameraPosition = camera.View.Position,
            SkyAmbient = SkyAmbient,
            GroundAmbient = GroundAmbient,
            KeyPosition = RackSpace.KeyLight,
            KeyColor = KeyColor,
            Light0Position = _space.Lights[0],
            Light0Color = lights.Lights.Left.Color * LightStrength,
            Light1Position = _space.Lights[1],
            Light1Color = lights.Lights.Right.Color * LightStrength,
        };
        var seconds = (float)time.RawDeltaTime.TotalSeconds;
        _outputLevel = rack.Audio.OutputLevel;
        _loudness = Loudness.Step(_loudness, _outputLevel, seconds);
        _outputReading = OutputMeter.Step(_outputReading, _outputLevel, seconds);
        _cableDrags.Clear();
        foreach (var drag in rackInput.Drags)
        {
            if (drag.Drag is RackDrag.Cable)
            {
                _cableDrags.Add(drag);
            }
        }

        if (passManager.SetInputs(_pass, room).TryPickProblems(out var problems)
            || finish.Render(room, _loudness).TryPickProblems(out problems)
            || scenery.Render((float)time.RawDeltaTime.TotalSeconds).TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to set the room's camera and lights");
        }

        foreach (var batch in _batches)
        {
            batch.Begin();
        }

        _ink.Begin();
        _grilles.Begin();
        if (!_drawnStill)
        {
            if (_layers.Draw(StillLayer, DrawStill).TryPickProblems(out problems))
            {
                return problems.Prepend("Failed to draw the room");
            }

            _drawnStill = true;
        }

        if (DrawRoom().TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to draw the room");
        }

        // What moves on the panels, of those that can be seen: the side case's once the seat turns toward it (or on a
        // portrait screen, whose tall view takes in a corner of it from the rack), and the rack's and the LIGHTS box's
        // until the seat has turned all the way
        var turn = seat.Seat.Turn;
        var sideHidden = turn == 0 && seat.Seat.Camera.Projection.AspectRatio >= 1;
        foreach (var panel in rackInput.Layout.Panels)
        {
            if (panel.Place == PanelPlace.Side ? sideHidden : turn == 1)
            {
                continue;
            }

            if (DrawPanel(panel).TryPickProblems(out problems))
            {
                return problems.Prepend("Failed to draw panel {0}", panel.Title);
            }
        }

        DrawKeys();
        if (DrawCheek().TryPickProblems(out problems) || DrawCables().TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to draw the cables");
        }

        foreach (var batch in _batches)
        {
            if (batch.End().TryPickProblems(out problems))
            {
                return problems.Prepend("Failed to draw the room");
            }
        }

        if (_grilles.End().TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to draw the speakers");
        }

        if (_ink.End().TryPickProblems(out problems) || DrawHint().TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to draw the panels' ink");
        }

        return renderingManager.Render();
    }

    // What stays put, drawn once into the still layer: the room, the case and its panels with their screws, jacks, titles
    // and labels, the scopes' cabinets and the cheek's slots and readouts. What moves or lights up is drawn each frame.
    private Result DrawStill()
    {
        if (DrawRoomStill().TryPickProblems(out var problems))
        {
            return problems;
        }

        foreach (var panel in rackInput.Layout.Panels)
        {
            DrawPanelStill(panel);
        }

        DrawCheekStill();
        return Result.Success();
    }

    private Result DrawRoomStill()
    {
        var panels = _space.Panels;
        var unitRight = Vector3.Normalize(panels.Right);
        var unitDown = Vector3.Normalize(panels.Down);
        var face = _space.Face;
        var faceSize = face.Size * RackSpace.Scale;
        _plinth.Add(Matrix4x4.CreateTranslation(0, 0.08f, -0.53f), Case, shine: 0.2f, grain: Grain);

        // The case behind the face, then its rim standing proud round the panels
        var layout = rackInput.Layout;
        (Box Area, float Height)[] parts =
        [
            (new Box(face.Min, new Vector2(face.Size.X, RackLayout.RackTop - face.Min.Y)), RimHeight),
            (new Box(new Vector2(face.Min.X, _space.PanelsHeight), new Vector2(face.Size.X, face.Max.Y - _space.PanelsHeight)), RimHeight),
            (new Box(new Vector2(face.Min.X, RackLayout.RackTop), new Vector2(RackSpace.Rim, _space.PanelsHeight - RackLayout.RackTop)), RimHeight),
            (new Box(new Vector2(face.Max.X - RackSpace.Rim, RackLayout.RackTop), new Vector2(RackSpace.Rim, _space.PanelsHeight - RackLayout.RackTop)), RimHeight),
        ];
        if (BoxOf(new Vector3(faceSize.X, faceSize.Y, 0.1f), 0.012f, occludes: true).TryPickProblems(out var problems, out var caseBox))
        {
            return problems;
        }

        // The case's body stops short of the face, which is solid but for the speakers' openings
        caseBox.Add(Frame(unitRight, -unitDown, panels.Normal, panels.ToWorld(face.Centre) - panels.Normal * (0.05f + SpeakerRecess)), Case, shine: 0.2f, grain: Grain);
        var (leftSpeaker, rightSpeaker) = (layout.Speakers[0], layout.Speakers[1]);
        Box[] solid =
        [
            new(face.Min, new Vector2(face.Size.X, leftSpeaker.Min.Y - face.Min.Y)),
            new(new Vector2(face.Min.X, leftSpeaker.Max.Y), new Vector2(face.Size.X, face.Max.Y - leftSpeaker.Max.Y)),
            new(new Vector2(face.Min.X, leftSpeaker.Min.Y), new Vector2(leftSpeaker.Min.X - face.Min.X, leftSpeaker.Size.Y)),
            new(new Vector2(leftSpeaker.Max.X, leftSpeaker.Min.Y), new Vector2(rightSpeaker.Min.X - leftSpeaker.Max.X, leftSpeaker.Size.Y)),
            new(new Vector2(rightSpeaker.Max.X, leftSpeaker.Min.Y), new Vector2(face.Max.X - rightSpeaker.Max.X, leftSpeaker.Size.Y)),
        ];
        foreach (var area in solid)
        {
            if (BoxOf(new Vector3(area.Size.X * RackSpace.Scale, area.Size.Y * RackSpace.Scale, SpeakerRecess), 0.001f).TryPickProblems(out problems, out var slab))
            {
                return problems;
            }

            slab.Add(Frame(unitRight, -unitDown, panels.Normal, panels.ToWorld(area.Centre) - panels.Normal * SpeakerRecess / 2), Case, shine: 0.2f, grain: Grain);
        }

        DrawSpeakersStill();
        foreach (var (area, height) in parts)
        {
            var size = new Vector3(area.Size.X * RackSpace.Scale, area.Size.Y * RackSpace.Scale, height);
            if (BoxOf(size, 0.004f).TryPickProblems(out problems, out var part))
            {
                return problems;
            }

            part.Add(Frame(unitRight, -unitDown, panels.Normal, panels.ToWorld(area.Centre) + panels.Normal * height / 2), Case, shine: 0.25f, grain: Grain);
        }

        // Between cards stacked in a column, the rails they screw into: brushed aluminium with the groove their nuts slide in
        foreach (var rail in layout.Rails)
        {
            var size = new Vector3(rail.Size.X * RackSpace.Scale, rail.Size.Y * RackSpace.Scale, PanelThickness * 0.7f);
            if (BoxOf(size, 0.0008f).TryPickProblems(out problems, out var bar)
                || BoxOf(new Vector3(size.X - 0.004f, 0.0016f, size.Z), 0.0005f).TryPickProblems(out problems, out var groove))
            {
                return problems;
            }

            var at = panels.ToWorld(rail.Centre) + panels.Normal * size.Z / 2;
            bar.Add(Frame(unitRight, -unitDown, panels.Normal, at), RailMetal, shine: 0.5f, finish: BrushedRail);
            groove.Add(Frame(unitRight, -unitDown, panels.Normal, at + panels.Normal * 0.0004f), JackHole, shine: 0.1f);
        }

        // Larger screws at the face's corners, on the rim
        foreach (var corner in Corners(face, RackSpace.Rim / 2))
        {
            DrawScrew(_caseScrew, corner, RimHeight, corner.X * 7.1f + corner.Y);
        }

        if (DrawKeybed().TryPickProblems(out problems) || DrawSideCaseStill().TryPickProblems(out problems))
        {
            return problems;
        }

        // The name, on the strip of the plinth between the panels and the keys
        var plate = new Surface(new Vector3(0, 0.114f, -0.4295f), Vector3.UnitX * RackSpace.Scale, -Vector3.UnitY * RackSpace.Scale, Vector3.UnitZ);
        var name = _ink.On(plate.ToMatrix());
        name.TextCentred(Vector2.Zero, Name, 3.4f, Ink);
        name.Glowing(0.6f).Circle(new Vector2(DotFont.Width(Name) * 3.4f / 2 + 14, 0), 3.2f, Accent);

        _posts.Add(Matrix4x4.Identity, JackMetal, shine: 0.6f);
        foreach (var (scope, i) in _space.Scopes.Select((s, i) => (s, i)))
        {
            if (DrawScopeStill(scope, i).TryPickProblems(out problems))
            {
                return problems;
            }
        }

        return Result.Success();
    }

    // What moves in the room: the speakers' cones, the scopes' screens and the jacks they glow round, and the lamps
    private Result DrawRoom()
    {
        DrawCones();
        foreach (var (scope, i) in _space.Scopes.Select((s, i) => (s, i)))
        {
            if (DrawScope(scope, i).TryPickProblems(out var problems))
            {
                return problems;
            }

            var light = i == 0 ? lights.Lights.Left : lights.Lights.Right;
            var glow = new Vector4(Vector3.Min(light.Color, Vector3.One), 1);
            _lamp.Add(Matrix4x4.CreateTranslation(_space.Lights[i]), Ink, shine: 0, emissive: glow);
        }

        return Result.Success();
    }

    // The keybed under the keys and the cheeks, the keyboard's cheek left of the keys and a narrow one right, a lip along
    // its front, and the LIGHTS box on the desk
    private Result DrawKeybed()
    {
        var layout = rackInput.Layout;
        var keys = _space.Keys;
        var keysMiddle = keys.ToWorld(new Vector2(layout.CaseWidth / 2, layout.Keys.Centre.Y - RackLayout.Margin / 2));
        _keybed.Add(Matrix4x4.CreateTranslation(0, 0.028f, keysMiddle.Z), Case, shine: 0.2f, grain: Grain);

        var depth = RackLayout.CheekDepth * RackSpace.Scale + 0.05f;
        var front = keys.ToWorld(new Vector2(0, layout.Keys.Max.Y)).Z + 0.025f;
        var left = _space.Face.Min.X * RackSpace.Scale - _space.Width / 2;
        var leftWidth = (layout.Keys.Min.X - _space.Face.Min.X - 2) * RackSpace.Scale;
        var rightWidth = (_space.Face.Max.X - layout.Keys.Max.X - 2) * RackSpace.Scale;
        var height = RackSpace.CheekTop - 0.048f;
        if (BoxOf(new Vector3(leftWidth, height, depth), 0.006f).TryPickProblems(out var problems, out var leftCheek)
            || BoxOf(new Vector3(rightWidth, height, depth), 0.006f).TryPickProblems(out problems, out var rightCheek)
            || BoxOf(new Vector3(CaseWidth, 0.014f, 0.016f), 0.006f).TryPickProblems(out problems, out var lip))
        {
            return problems;
        }

        leftCheek.Add(Matrix4x4.CreateTranslation(left + leftWidth / 2, 0.048f + height / 2, front - depth / 2), Case, shine: 0.25f, grain: Grain);
        rightCheek.Add(Matrix4x4.CreateTranslation(-left - rightWidth / 2, 0.048f + height / 2, front - depth / 2), Case, shine: 0.25f, grain: Grain);
        lip.Add(Matrix4x4.CreateTranslation(0, 0.048f + 0.007f, front - 0.008f), Case, shine: 0.25f, grain: Grain);
        return DrawDeskBox();
    }

    // The side case on the desk's return: a slab under its sloping face with a rim round the cards, on a block at the
    // back and a lip at the front, so it stands as a wedge; screws at the rim's corners
    private Result DrawSideCaseStill()
    {
        if (rackInput.Layout.Side is not { } area || _space.Side is not { } face)
        {
            return Result.Success();
        }

        var right = Vector3.Normalize(face.Right);
        var down = Vector3.Normalize(face.Down);
        var size = area.Size * RackSpace.Scale;
        const float Thickness = 0.03f;
        var forward = Vector3.Normalize(new Vector3(down.X, 0, down.Z));
        var top = face.ToWorld(new Vector2(area.Centre.X, area.Min.Y)) - face.Normal * Thickness;
        var bottom = face.ToWorld(new Vector2(area.Centre.X, area.Max.Y)) - face.Normal * Thickness;
        if (BoxOf(new Vector3(size.X, size.Y, Thickness), 0.008f).TryPickProblems(out var problems, out var slab)
            || BoxOf(new Vector3(size.X, top.Y, 0.05f), 0.006f).TryPickProblems(out problems, out var back)
            || BoxOf(new Vector3(size.X, bottom.Y, 0.03f), 0.006f).TryPickProblems(out problems, out var front))
        {
            return problems;
        }

        slab.Add(Frame(right, -down, face.Normal, face.ToWorld(area.Centre) - face.Normal * (Thickness / 2)), Case, shine: 0.2f, grain: Grain);
        var upright = Vector3.Cross(right, Vector3.UnitY);
        // The block stands behind the face's top edge, so it never pokes through the face
        back.Add(Frame(right, Vector3.UnitY, upright, new Vector3(top.X, top.Y / 2, top.Z) - forward * 0.025f), Case, shine: 0.2f, grain: Grain);
        front.Add(Frame(right, Vector3.UnitY, upright, new Vector3(bottom.X, bottom.Y / 2, bottom.Z) - forward * 0.012f), Case, shine: 0.2f, grain: Grain);

        // The rim round the cards along the face's edge, standing proud of them, with a screw at each corner
        const float R = RackSpace.Rim;
        Box[] rims =
        [
            new(area.Min, new Vector2(area.Size.X, R)),
            new(new Vector2(area.Min.X, area.Max.Y - R), new Vector2(area.Size.X, R)),
            new(new Vector2(area.Min.X, area.Min.Y + R), new Vector2(R, area.Size.Y - 2 * R)),
            new(new Vector2(area.Max.X - R, area.Min.Y + R), new Vector2(R, area.Size.Y - 2 * R)),
        ];
        foreach (var rim in rims)
        {
            if (BoxOf(new Vector3(rim.Size.X * RackSpace.Scale, rim.Size.Y * RackSpace.Scale, RimHeight), 0.004f).TryPickProblems(out problems, out var part))
            {
                return problems;
            }

            part.Add(Frame(right, -down, face.Normal, face.ToWorld(rim.Centre) + face.Normal * RimHeight / 2), Case, shine: 0.25f, grain: Grain);
        }

        foreach (var corner in Corners(area, R / 2))
        {
            DrawScrew(_caseScrew, corner, RimHeight, corner.X * 7.1f + corner.Y);
        }

        return Result.Success();
    }

    // The LIGHTS box: a slab under its sloping face, standing on a block at the back
    private Result DrawDeskBox()
    {
        if (rackInput.Layout.Desk is not { } panel || _space.Desk is not { } face)
        {
            return Result.Success();
        }

        var right = Vector3.Normalize(face.Right);
        var down = Vector3.Normalize(face.Down);
        var size = panel.Bounds.Size * RackSpace.Scale;
        const float Thickness = 0.045f;
        var back = face.ToWorld(new Vector2(panel.Bounds.Centre.X, panel.Bounds.Min.Y)) - face.Normal * Thickness;
        var forward = Vector3.Normalize(new Vector3(down.X, 0, down.Z));
        if (BoxOf(new Vector3(size.X + 0.016f, size.Y + 0.016f, Thickness), 0.008f).TryPickProblems(out var problems, out var slab)
            || BoxOf(new Vector3(size.X + 0.016f, back.Y, 0.05f), 0.006f).TryPickProblems(out problems, out var block))
        {
            return problems;
        }

        slab.Add(Frame(right, -down, face.Normal, face.ToWorld(panel.Bounds.Centre) - face.Normal * (Thickness / 2 - PanelThickness)), Case, shine: 0.2f, grain: Grain);
        block.Add(Frame(right, Vector3.UnitY, Vector3.Cross(right, Vector3.UnitY), new Vector3(back.X, back.Y / 2, back.Z) + forward * 0.025f), Case, shine: 0.2f, grain: Grain);
        return Result.Success();
    }

    // A CRT in a deep cabinet that narrows to the back, its bulging glass sunk behind a thick bezel; under the screen its
    // label, two small knobs and its input jack
    private Result DrawScopeStill(ScopePlacement scope, int index)
    {
        var (x, y, z) = scope.Axes;
        var centre = scope.Centre;
        var cabinetCentre = centre - y * CrtCabinet.Drop;
        const float Width = CrtCabinet.Width, Height = CrtCabinet.Height, Depth = CrtCabinet.BezelDepth;
        const float ScreenWidth = CrtCabinet.ScreenWidth, ScreenHeight = CrtCabinet.ScreenHeight;
        // The cabinet's body, which hides the window's lights behind it, and the bezel round the glowing screen
        (Vector3 Size, Vector3 At, float Bevel, bool Occludes)[] shell =
        [
            (new Vector3(Width, Height, 0.13f), cabinetCentre - z * (Depth + 0.065f), 0.016f, true),
            (new Vector3(Width * 0.7f, Height * 0.68f, 0.16f), cabinetCentre + y * 0.008f - z * (Depth + 0.13f + 0.07f), 0.022f, true),
            // The bezel: the bars round the opening
            (new Vector3(Width, CrtCabinet.BezelTop, Depth), centre + y * (ScreenHeight + CrtCabinet.BezelTop) / 2 - z * Depth / 2, 0.007f, false),
            (new Vector3(Width, CrtCabinet.BezelBottom, Depth), centre - y * (ScreenHeight + CrtCabinet.BezelBottom) / 2 - z * Depth / 2, 0.007f, false),
            (new Vector3(CrtCabinet.BezelSide, ScreenHeight + 0.002f, Depth), centre - x * (ScreenWidth + CrtCabinet.BezelSide) / 2 - z * Depth / 2, 0.006f, false),
            (new Vector3(CrtCabinet.BezelSide, ScreenHeight + 0.002f, Depth), centre + x * (ScreenWidth + CrtCabinet.BezelSide) / 2 - z * Depth / 2, 0.006f, false),
        ];
        foreach (var (size, at, bevel, occludes) in shell)
        {
            if (BoxOf(size, bevel, occludes).TryPickProblems(out var problems, out var part))
            {
                return problems;
            }

            part.Add(Frame(x, y, z, at), Case, shine: 0.25f, grain: Grain);
        }

        var screen = scopes.Screens[index];
        var bezel = scope.Bezel;
        var barHeight = CrtCabinet.BezelBottom / RackSpace.Scale;
        var ink = _ink.On(bezel.ToMatrix(0.0004f));
        ink.TextCentred(new Vector2(70, barHeight / 2 - 7), index == 0 ? "SCOPE L" : "SCOPE R", 2, Soft);
        ink.TextCentred(new Vector2(70, barHeight / 2 + 12), $"{screen.MillisecondsPerDivision:0} MS/DIV", LabelDot, Muted);
        foreach (var (label, at) in new[] { ("FOCUS", 175f), ("INTEN", 235f) })
        {
            var knobAt = bezel.ToWorld(new Vector2(at, barHeight / 2 - 8));
            _knob.Add(Matrix4x4.CreateScale(0.5f) * Frame(x, z, -y, knobAt), KnobBody, shine: 0.55f, finish: KnobFinish);
            ink.TextCentred(new Vector2(at, barHeight / 2 + 17), label, LabelDot, Soft);
        }

        DrawJackMesh(scope.Jack, x, z, -y);
        ink.TextCentred(CrtCabinet.Jack + new Vector2(0, 25), "IN", LabelDot, Soft);
        return Result.Success();
    }

    // A scope's glass lit by its trace, and its input jack glowing when a cable can plug into it
    private Result DrawScope(ScopePlacement scope, int index)
    {
        var (x, y, z) = scope.Axes;
        var screen = scopes.Screens[index];
        if (scopeScreens.Draw(index, screen, Frame(x, y, z, scope.Centre - z * (CrtCabinet.BezelDepth - 0.004f)), GlassWidth, GlassHeight)
            .TryPickProblems(out var drawProblems))
        {
            return drawProblems.Prepend("Failed to draw a scope's screen");
        }

        if (_scopeJacks[index] is { } layout && JackHighlight(layout) is { } glow)
        {
            var ink = _ink.On(scope.Bezel.ToMatrix(0.0004f));
            ink.Glowing().Circle(CrtCabinet.Jack, RackLayout.JackRadius + 5, glow, RackLayout.JackRadius + 2);
            ink.Glowing(0);
        }

        return Result.Success();
    }

    // Each speaker: its panel with the round opening, the dark metal rim round it, the hole the cone sits in, and the
    // domed grille over it
    private void DrawSpeakersStill()
    {
        var panels = _space.Panels;
        var right = Vector3.Normalize(panels.Right);
        var down = Vector3.Normalize(panels.Down);
        foreach (var speaker in rackInput.Layout.Speakers)
        {
            var centre = panels.ToWorld(speaker.Centre);
            _speakerPlate.Add(Frame(right, -down, panels.Normal, centre), Plate, shine: 0.3f);
            var axis = Frame(right, panels.Normal, down, centre);
            _speakerWall.Add(axis, JackHole, shine: 0.1f);
            _speakerRim.Add(axis, DarkMetal, shine: 0.7f);
            _grilles.Add(new Shaders.Grille.Instance(Matrix4x4.CreateTranslation(0, PanelThickness + 0.0004f, 0) * axis, GrillePaint, new Vector4(WirePitch, WireShare, 0, 0)));
            DrawPanelScrews(speaker);
        }
    }

    // The speakers' cones, recessed behind the grilles, breathing a little with the output
    private void DrawCones()
    {
        var panels = _space.Panels;
        var right = Vector3.Normalize(panels.Right);
        var down = Vector3.Normalize(panels.Down);
        var push = float.Min(_outputLevel, 1) * 0.003f;
        foreach (var speaker in rackInput.Layout.Speakers)
        {
            _cone.Add(Matrix4x4.CreateTranslation(0, push, 0) * Frame(right, panels.Normal, down, panels.ToWorld(speaker.Centre)), ConePaper, shine: 0.05f);
        }
    }

    private void DrawPanelScrews(Box bounds)
    {
        foreach (var corner in Corners(bounds, 6))
        {
            DrawScrew(_screw, corner, PanelThickness, corner.X * 3.7f + corner.Y * 1.3f);
        }
    }

    private static IEnumerable<Vector2> Corners(Box box, float inset) =>
    [
        box.Min + new Vector2(inset), new Vector2(box.Max.X - inset, box.Min.Y + inset),
        new Vector2(box.Min.X + inset, box.Max.Y - inset), box.Max - new Vector2(inset),
    ];

    // A screw's domed head at a design point, standing off the face by the height, its slot turned by the angle
    private void DrawScrew(Batch head, Vector2 design, float height, float angle)
    {
        var panels = _space.SurfaceAt(design);
        var frame = Frame(Vector3.Normalize(panels.Right), panels.Normal, Vector3.Normalize(panels.Down), panels.ToWorld(design) + panels.Normal * height);
        head.Add(frame, JackMetal, shine: 0.65f, finish: BareMetal);
        var radius = head == _caseScrew ? 0.0034f : 0.0021f;
        // A Phillips cross, each screw turned its own way
        foreach (var arm in new[] { 0f, MathF.PI / 2 })
        {
            var slot = Matrix4x4.CreateScale(radius * 1.25f, 0.0005f, radius * 0.26f)
                       * Matrix4x4.CreateRotationY(angle + arm)
                       * Matrix4x4.CreateTranslation(0, radius * 0.8f, 0)
                       * frame;
            _pointer.Add(slot, JackHole, shine: 0);
        }
    }

    private void DrawPanelStill(PanelLayout panel)
    {
        var panels = _space.SurfaceAt(panel.Bounds.Centre);
        var unitRight = Vector3.Normalize(panels.Right);
        var unitDown = Vector3.Normalize(panels.Down);
        _panelBoxes[panel.Bounds.Size].Add(new Shaders.Lit.Instance(
            Frame(unitRight, -unitDown, panels.Normal, panels.ToWorld(panel.Bounds.Centre) + panels.Normal * PanelThickness / 2), Panel with { W = 0.35f }, Vector4.Zero, BrushedPlate));

        DrawPanelScrews(panel.Bounds);
        var ink = _ink.On(panels.ToMatrix(InkLift));
        ink.Box(panel.Bounds.Min + new Vector2(1), panel.Bounds.Max - new Vector2(1), PanelEdge, 7, outline: 1.2f);
        if (panel.Place != PanelPlace.Desk)
        {
            // A card: a thin stripe of its group's colour across the top, its heading under it at the left
            ink.Box(panel.Bounds.Min + new Vector2(3, 3), new Vector2(panel.Bounds.Max.X - 3, panel.Bounds.Min.Y + 6), GroupColors[(int)panel.Group] with { W = 0.85f }, 1.5f);
            ink.TextCentred(panel.Bounds.Min + new Vector2(PanelTitleInset + DotFont.Width(panel.Title) * CardTitleDot / 2, RackLayout.CardTitleY), panel.Title, CardTitleDot, Ink);
        }
        else
        {
            ink.TextCentred(panel.Bounds.Min + new Vector2(panel.Bounds.Size.X / 2, RackLayout.TitleY), panel.Title, TitleDot, Ink);
        }

        foreach (var caption in panel.Captions)
        {
            ink.TextCentred(caption.Centre + new Vector2(DotFont.Width(caption.Text) * LabelDot / 2, 0), caption.Text, LabelDot, Soft);
        }

        foreach (var knob in panel.Knobs)
        {
            if (knob.Slot is { } slot)
            {
                DrawFaderStill(slot);
            }
            else
            {
                DrawKnobStill(knob);
            }
        }

        foreach (var jack in panel.Jacks)
        {
            DrawJackStill(jack);
        }

        if (panel.Meter is { } meter)
        {
            ink = _ink.On(_space.SurfaceAt(meter.Centre).ToMatrix(InkLift));
            ink.Box(meter.Min - new Vector2(3), meter.Max + new Vector2(3, -9), JackHole, 3);
            ink.TextCentred(new Vector2(meter.Centre.X, meter.Max.Y + 2), "LEVEL", LabelDot, Soft);
        }

        if (panel.Place == PanelPlace.Desk)
        {
            DrawLightCaptionsStill(panel);
        }

        // The sequencer's step lights, unlit, each a little domed lens, a step's number under its knob
        ink = _ink.On(_space.SurfaceAt(panel.Bounds.Centre).ToMatrix(InkLift));
        foreach (var light in panel.StepLights)
        {
            ink.Lens(light, StepLight, LevelOff);
        }

        if (panel.Readout is { } readout)
        {
            // A segment display: its glass, every cell's dots faintly there
            ink.Box(readout.Min - new Vector2(3), readout.Max + new Vector2(3), PanelEdge, 5);
            ink.Box(readout.Min, readout.Max, SegmentGlass, 3);
            ink.Cells(readout.Centre, (int)((readout.Size.X - 8) / (DotFont.Advance * ReadoutDot)), ReadoutDot, SegmentGhost);
        }
    }

    // The sequencer's step lights and the quantizer's readout, in design units: a light's size, and the readout's dots
    private const float StepLight = 2.6f, ReadoutDot = 1.5f;

    private Result DrawPanel(PanelLayout panel)
    {
        foreach (var knob in panel.Knobs)
        {
            DrawKnob(knob);
        }

        foreach (var jack in panel.Jacks)
        {
            DrawJack(jack);
        }

        if (panel.Meter is { } meter)
        {
            DrawMeter(meter);
        }

        if (panel.Place == PanelPlace.Desk)
        {
            DrawLightCaptions(panel);
        }

        if (panel.StepLights.Count > 0 && rack.Audio.SequencerStep(panel.Module.Id) is { } step && step < panel.StepLights.Count)
        {
            // The light of the step heard, chasing along the steps
            _ink.On(_space.SurfaceAt(panel.Bounds.Centre).ToMatrix(InkLift)).Glowing().Lens(panel.StepLights[step], StepLight, LevelOn);
            _ink.Glowing(0);
        }

        if (panel.Readout is not null)
        {
            DrawReadout(panel);
        }

        return panel.Display is not null ? DrawPicker(panel) : Result.Success();
    }

    // The quantizer's scale named on its readout, drawn again only when the scale changes
    private void DrawReadout(PanelLayout panel)
    {
        var scale = (int)rack.Patch.GetValue(panel.Module.Id, Modules.Scale);
        _layers.Draw(Memo<int>.Of(_readoutMemos, panel), scale, (this, panel, scale), static s =>
        {
            var readout = s.panel.Readout!.Value;
            var name = Modules.Scales[int.Clamp(s.scale, 0, Modules.Scales.Length - 1)].Name;
            s.Item1._ink.On(s.Item1._space.SurfaceAt(readout.Centre).ToMatrix(InkLift)).Glowing(0.8f).TextCentred(readout.Centre, name, ReadoutDot, SegmentLit);
            s.Item1._ink.Glowing(0);
        });
    }

    // The knob's soft shadow on the panel, and the track its arc lights up along; a button's dark well
    private void DrawKnobStill(KnobLayout knob)
    {
        var ink = _ink.On(_space.SurfaceAt(knob.Centre).ToMatrix(InkLift));
        if (knob.Button is { } cap)
        {
            ink.Shadow(cap.Centre, cap.Size.X * 0.75f, 0.5f, solid: 0.6f);
            ink.Box(cap.Min - new Vector2(2.5f), cap.Max + new Vector2(2.5f), JackHole, 4);
            return;
        }

        ink.Shadow(knob.Centre, knob.Radius * 1.4f, 0.6f, solid: 0.75f);
        ink.ArcRing(knob.Centre, knob.Radius + 6, knob.Radius + 2.5f, -KnobSweep, KnobSweep, KnobTrack);
    }

    // A knob's arc, label, cap and pointer, drawn again only when its value or whether it is active changes
    private void DrawKnob(KnobLayout knob)
    {
        var value = rack.Patch.GetValue(knob.Module, knob.Parameter.Name);
        var active = IsDragged(knob) || rackInput.Drags.Count == 0 && rackInput.Hovered is RackHit.Knob hovered && hovered.Layout == knob;
        _layers.Draw(Memo<(float, bool)>.Of(_knobMemos, knob), (value, active), (this, knob, value, active),
            static s =>
            {
                if (s.knob.IsButton)
                {
                    s.Item1.DrawToggle(s.knob, s.value, s.active);
                }
                else if (s.knob.IsFader)
                {
                    s.Item1.DrawFader(s.knob, s.value, s.active);
                }
                else
                {
                    s.Item1.DrawKnob(s.knob, s.value, s.active);
                }
            });
    }

    // A fader's slot with dark felt down it, edged, and tick marks beside it
    private void DrawFaderStill(Box slot)
    {
        var ink = _ink.On(_space.SurfaceAt(slot.Centre).ToMatrix(InkLift));
        ink.Box(slot.Min - new Vector2(2.5f), slot.Max + new Vector2(2.5f), KnobTrack, 4);
        ink.Box(slot.Min, slot.Max, JackHole, 3);
        ink.Box(slot.Min + new Vector2(2), slot.Max - new Vector2(2), Felt, 2);
        for (var i = 0; i <= 4; i++)
        {
            var y = slot.Max.Y - slot.Size.Y * i / 4;
            ink.Segment(new Vector2(slot.Max.X + 6, y), new Vector2(slot.Max.X + 11, y), 1, KnobTrack);
        }
    }

    // A fader's travel lit up to its value, its name under the slot, and the ribbed cap riding the slot at the value
    private void DrawFader(KnobLayout fader, float value, bool active)
    {
        var slot = fader.Slot!.Value;
        var parameter = fader.Parameter;
        var turn = Knobs.ToTurn(parameter, value);
        var surface = _space.SurfaceAt(slot.Centre);
        var ink = _ink.On(surface.ToMatrix(InkLift));
        ink.Glowing().Segment(new Vector2(slot.Min.X - 7, slot.Max.Y), new Vector2(slot.Min.X - 7, slot.Max.Y - slot.Size.Y * turn), 1.6f, Accent);
        ink.Glowing(0);
        var label = parameter.HasChoices || active ? Knobs.Format(parameter, value) : parameter.Caption;
        ink.TextCentred(fader.Label ?? new Vector2(slot.Centre.X, slot.Max.Y + 12), label, LabelDot, active ? Accent : Soft);

        var cap = slot.Max.Y - slot.Size.Y * turn;
        ink.Shadow(new Vector2(slot.Centre.X, cap + 2), 16, 0.55f, solid: 0.5f);
        var frame = Frame(Vector3.Normalize(surface.Right), surface.Normal, Vector3.Normalize(surface.Down),
            surface.ToWorld(new Vector2(slot.Centre.X, cap)) + surface.Normal * PanelThickness);
        // The cap: a block with ridges across its top for the thumb, the middle one cream
        _faderCap.Add(Matrix4x4.CreateTranslation(0, FaderCapSize.Y / 2, 0) * frame, active ? Rgb(0x2A2D36) : KnobBody, shine: 0.5f, finish: KnobFinish with { Z = 0.004f });
        for (var i = -2; i <= 2; i++)
        {
            _faderRib.Add(Matrix4x4.CreateTranslation(0, FaderCapSize.Y + 0.0002f, i * 0.0021f) * frame, i == 0 ? Ink : KnobBody, shine: 0.5f);
        }
    }

    // A push button: its cap, its LED lit amber while its switch is on, and, for one among knobs, its setting under it
    private void DrawToggle(KnobLayout toggle, float value, bool active)
    {
        var cap = toggle.Button!.Value;
        var surface = _space.SurfaceAt(cap.Centre);
        var on = value >= toggle.Parameter.Min + 0.5f;
        _toggleCap.Add(Frame(Vector3.Normalize(surface.Right), surface.Normal, Vector3.Normalize(surface.Down),
            surface.ToWorld(cap.Centre) + surface.Normal * (PanelThickness + ToggleHeight / 2)), active ? KnobTrack : ButtonCap, shine: 0.35f);
        var top = _ink.On(surface.ToMatrix(PanelThickness + ToggleHeight + 0.0004f));
        top.Glowing(on ? 1 : 0).Lens(cap.Centre, 3, on ? Accent : MeterOff);
        top.Glowing(0);
        if (toggle.Label is { } label)
        {
            _ink.On(surface.ToMatrix(InkLift)).TextCentred(label, Knobs.Format(toggle.Parameter, value), LabelDot, active || on ? Accent : Soft);
        }
    }

    private void DrawKnob(KnobLayout knob, float value, bool active)
    {
        var parameter = knob.Parameter;
        var turn = Knobs.ToTurn(parameter, value);
        var centre = knob.Centre;
        var radius = knob.Radius;

        // The arc round the knob lights up to its turn
        var ink = _ink.On(_space.SurfaceAt(centre).ToMatrix(InkLift));
        ink.Glowing();
        if (parameter.Scale == ParameterScale.Stepped)
        {
            var steps = (int)(parameter.Max - parameter.Min);
            var at = -KnobSweep + 2 * KnobSweep * turn;
            var halfWidth = MathF.Min(0.18f, KnobSweep / int.Max(1, steps));
            ink.ArcRing(centre, radius + 6, radius + 2.5f, at - halfWidth, at + halfWidth, Accent);
        }
        else
        {
            ink.ArcRing(centre, radius + 6, radius + 2.5f, -KnobSweep, -KnobSweep + 2 * KnobSweep * turn, Accent);
        }

        ink.Glowing(0);
        var label = (parameter.HasChoices && !knob.Named) || active ? Knobs.Format(parameter, value) : parameter.Caption;
        ink.TextCentred(knob.Label ?? centre + new Vector2(0, knob.Radius + 17), label, LabelDot, active ? Accent : Soft);

        // The cap stands out of the panel, its pointer line set into its domed top, turned with the knob; a big knob has a
        // metal insert on top, the line cut into it
        var big = radius > RackLayout.KnobRadius * 1.2f;
        var frame = Matrix4x4.CreateScale(radius / RackLayout.KnobRadius) * OnPanel(centre);
        _knob.Add(frame, active ? Rgb(0x2A2D36) : KnobBody, shine: 0.55f, finish: KnobFinish);
        if (big)
        {
            _insert.Add(frame, Aluminium, shine: 0.6f, finish: CapInsert);
        }

        var angle = -KnobSweep + 2 * KnobSweep * turn;
        var length = RackLayout.KnobRadius * RackSpace.Scale * (big ? 0.46f : 0.6f);
        var pointer = Matrix4x4.CreateTranslation(-0.5f, -0.5f, -0.5f)
                      * Matrix4x4.CreateScale(0.0012f, 0.0006f, length)
                      * Matrix4x4.CreateTranslation(0, big ? 0.0201f : 0.0193f, -length / 2 - 0.0008f)
                      * Matrix4x4.CreateRotationY(-angle)
                      * frame;
        _pointer.Add(pointer, big ? JackHole : Ink, shine: 0.2f);
    }

    private bool IsDragged(KnobLayout knob)
    {
        foreach (var drag in rackInput.Drags)
        {
            if (drag.Drag is RackDrag.Knob dragged && dragged.Layout == knob)
            {
                return true;
            }
        }

        return false;
    }

    // A jack's nut and sleeve, an output's thin cream border, and its label
    private void DrawJackStill(JackLayout jack, bool labelled = true)
    {
        var panels = _space.SurfaceAt(jack.Centre);
        DrawJackMesh(panels.ToWorld(jack.Centre) + panels.Normal * PanelThickness, Vector3.Normalize(panels.Right), panels.Normal, Vector3.Normalize(panels.Down),
            jack.Radius / RackLayout.JackRadius);

        var ink = _ink.On(panels.ToMatrix(InkLift));
        ink.Shadow(jack.Centre, jack.Radius * 1.35f, 0.5f, solid: 0.7f);
        var output = jack.Definition.Direction == JackDirection.Output;
        if (output)
        {
            // Its border, and its ring of lights, unlit
            ink.Circle(jack.Centre, jack.Radius + LevelBorder + 0.8f, Ink with { W = 0.85f }, jack.Radius + LevelBorder);
            for (var i = 0; i < OutputMeter.Lights; i++)
            {
                // Each light a little domed lens, lit from inside as the level reaches it
                ink.Lens(LevelLightAt(jack, i), LevelLight, LevelOff);
            }
        }

        if (jack.Definition.IsKnobInput || !labelled)
        {
            // A knob's CV input goes unlabelled: it sits under the knob it turns
            return;
        }

        ink.TextCentred(jack.LabelAt ?? jack.Centre - new Vector2(0, jack.Radius + (output ? LevelRing + 7 : 10)), jack.Label ?? jack.Definition.Name.ToUpperInvariant(), LabelDot, output ? Ink : Soft);
    }

    // An output's ring of level lights, and the glow round a jack a cable can plug into
    // An output's lit level lights and a jack's glow; not kept in a memo, as an output's level changes every frame while it sounds
    private void DrawJack(JackLayout jack)
    {
        var output = jack.Definition.Direction == JackDirection.Output;
        var highlight = JackHighlight(jack);
        if (!output && highlight is null)
        {
            return;
        }

        var ink = _ink.On(_space.SurfaceAt(jack.Centre).ToMatrix(InkLift));
        if (output)
        {
            DrawLevelRing(ink, jack);
        }

        if (highlight is { } glow)
        {
            ink.Glowing().Circle(jack.Centre, jack.Radius + 5, glow, jack.Radius + 2);
            ink.Glowing(0);
        }
    }

    // Round an output, a ring of tiny white lights on an arc open at the bottom that light up with the level there
    private void DrawLevelRing(InkCanvas ink, JackLayout jack)
    {
        var lit = levels.Lit(jack.Jack);
        for (var i = 0; i < OutputMeter.Lights && i < lit; i++)
        {
            var on = float.Min(lit - i, 1);
            ink.Glowing(on).Lens(LevelLightAt(jack, i), LevelLight, LevelOn with { W = on });
        }

        ink.Glowing(0);
    }

    private static Vector2 LevelLightAt(JackLayout jack, int light)
    {
        var angle = -LevelArc + 2 * LevelArc * light / (OutputMeter.Lights - 1);
        return jack.Centre + new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * (jack.Radius + LevelRing);
    }

    // A jack: a hex nut, a sleeve and its dark hole, standing out along the normal
    private void DrawJackMesh(Vector3 at, Vector3 right, Vector3 normal, Vector3 down, float scale = 1)
    {
        var frame = Matrix4x4.CreateScale(scale) * Frame(right, normal, down, at);
        _nut.Add(frame, JackMetal, shine: 0.7f, finish: JackFinish);
        _sleeve.Add(frame, JackMetal, shine: 0.8f, finish: BareMetal);
        _hole.Add(frame, JackHole, shine: 0);
    }

    // While cables are dragged, the jacks they can plug into glow, brightest under a cable's end; otherwise the jack
    // under the pointer does
    private Vector4? JackHighlight(JackLayout jack)
    {
        if (_cableDrags.Count > 0)
        {
            var pluggable = false;
            foreach (var drag in _cableDrags)
            {
                var cable = (RackDrag.Cable)drag.Drag;
                if (jack.Jack == cable.Fixed || rack.Patch.Plan(cable.Fixed, jack.Jack).TryPickProblems(out _))
                {
                    continue;
                }

                if (drag.Hovered is RackHit.Jack hovered && hovered.Layout == jack)
                {
                    return Accent;
                }

                pluggable = true;
            }

            return pluggable ? Accent with { W = 0.45f } : null;
        }

        return rackInput.Drags.Count == 0 && rackInput.Hovered is RackHit.Jack over && over.Layout == jack ? Soft : null;
    }

    // The input playing, or why none does, on a small green segment display, and its step buttons over it
    private Result DrawPicker(PanelLayout panel)
    {
        if (panel.Display is not { } display)
        {
            return Result.Success();
        }

        var status = rack.Audio.LineIn(panel.Module.Id);
        var hovered = rackInput.Drags.Count == 0 && rackInput.Hovered is RackHit.Picker over && over.Panel == panel;
        var ink = _ink.On(_space.Panels.ToMatrix(InkLift));
        ink.Box(display.Min - new Vector2(3), display.Max + new Vector2(3), hovered ? KnobTrack : PanelEdge, 5);
        ink.Box(display.Min, display.Max, SegmentGlass, 3);
        var color = status.State switch
        {
            LineInState.Live => SegmentLit,
            LineInState.Denied or LineInState.NoInput => Warning,
            _ => (SegmentLit * 0.75f) with { W = 1 },
        };
        var fits = (int)((display.Size.X - 8) / (DotFont.Advance * LabelDot));
        var lines = display.Size.Y / 4;
        // A segment display: every cell's dots faintly there, the lit ones over them
        var caption = new Vector2(display.Centre.X, display.Min.Y + lines + 2);
        var name = new Vector2(display.Centre.X, display.Max.Y - lines);
        ink.Cells(caption, (int)((display.Size.X - 8) / (DotFont.Advance * 2)), 2, SegmentGhost);
        ink.Cells(name, fits, LabelDot, SegmentGhost);
        ink.Glowing(0.8f).TextCentred(caption, LineInput.Caption(status), 2, color);
        ink.TextCentred(name, LineInput.Text(status, fits), LabelDot, color);
        ink.Glowing(0);
        foreach (var button in panel.Buttons)
        {
            if (DrawButton(button, button.Action == ButtonAction.PreviousInput ? -1 : 1, "").TryPickProblems(out var problems))
            {
                return problems;
            }
        }

        return Result.Success();
    }

    // A small square button standing off its surface, with a triangle pointing left or right on it, or a label
    private Result DrawButton(ButtonLayout button, int arrow, string label)
    {
        var surface = _space.SurfaceAt(button.Bounds.Centre);
        var pressed = rackInput.Hovered is RackHit.Button over && over.Layout == button && rackInput.Drags.Count == 0;
        var size = new Vector3(button.Bounds.Size.X * RackSpace.Scale, 0.005f, button.Bounds.Size.Y * RackSpace.Scale);
        if (BoxOf(size, 0.0015f).TryPickProblems(out var problems, out var cap))
        {
            return problems.Prepend("Failed to make a button's cap");
        }

        var frame = Frame(Vector3.Normalize(surface.Right), surface.Normal, Vector3.Normalize(surface.Down),
            surface.ToWorld(button.Bounds.Centre) + surface.Normal * (PanelThickness + size.Y / 2));
        cap.Add(frame, pressed ? KnobTrack : ButtonCap, shine: 0.35f);
        var ink = _ink.On(surface.ToMatrix(PanelThickness + size.Y + 0.0004f));
        var centre = button.Bounds.Centre;
        if (arrow != 0)
        {
            // A triangle, from the segments of its outline filled in
            for (var i = 0; i < 6; i++)
            {
                var x = centre.X - arrow * (5 - i * 1.6f);
                var half = 5 - i * 0.85f;
                ink.Segment(new Vector2(x, centre.Y - half), new Vector2(x, centre.Y + half), 1.8f, Ink);
            }
        }
        else
        {
            ink.TextCentred(centre, label, 2, Ink);
        }

        return Result.Success();
    }

    // Over each light's three knobs, a line naming the side with a lamp by it
    private void DrawLightCaptionsStill(PanelLayout panel)
    {
        var ink = _ink.On(_space.SurfaceAt(panel.Bounds.Centre).ToMatrix(InkLift));
        foreach (var side in (ReadOnlySpan<LightSide>)[LightSide.Left, LightSide.Right])
        {
            var y = panel.Bounds.Min.Y + RackLayout.LightCaptionY(side);
            var left = panel.Bounds.Min.X + RackLayout.PanelPadding;
            var right = panel.Bounds.Max.X - RackLayout.PanelPadding;
            var text = LightCaption(side);
            var middle = (left + right) / 2;
            ink.Segment(new Vector2(left, y), new Vector2(middle - 30, y), 1.2f, KnobTrack);
            ink.Segment(new Vector2(middle + 30, y), new Vector2(right, y), 1.2f, KnobTrack);
            ink.TextCentred(new Vector2(middle - 7, y), text, LabelDot, Soft);
            ink.Circle(CaptionLamp(panel, side), 6, Plate);
        }
    }

    // Each caption's lamp glowing its light's colour now
    private void DrawLightCaptions(PanelLayout panel)
    {
        var ink = _ink.On(_space.SurfaceAt(panel.Bounds.Centre).ToMatrix(InkLift)).Glowing();
        ink.Circle(CaptionLamp(panel, LightSide.Left), 4, new Vector4(Vector3.Min(lights.Lights.Left.Color, Vector3.One), 1));
        ink.Circle(CaptionLamp(panel, LightSide.Right), 4, new Vector4(Vector3.Min(lights.Lights.Right.Color, Vector3.One), 1));
        ink.Glowing(0);
    }

    private static string LightCaption(LightSide side) => side == LightSide.Left ? "LEFT" : "RIGHT";

    private static Vector2 CaptionLamp(PanelLayout panel, LightSide side)
    {
        var middle = (panel.Bounds.Min.X + panel.Bounds.Max.X) / 2;
        return new Vector2(middle + DotFont.Width(LightCaption(side)) * LabelDot / 2 + 1, panel.Bounds.Min.Y + RackLayout.LightCaptionY(side));
    }

    // A column of amber lights for the output's level, from the bottom
    private void DrawMeter(Box meter)
    {
        const int Lights = 12;
        var lit = (int)MathF.Ceiling(OutputMeter.Lit(_outputReading) / OutputMeter.Lights * Lights);
        var step = (meter.Size.Y - 12) / Lights;
        var ink = _ink.On(_space.SurfaceAt(meter.Centre).ToMatrix(InkLift));
        for (var i = 0; i < Lights; i++)
        {
            var on = i < lit;
            var bottom = meter.Max.Y - 12 - i * step;
            var color = on ? (i >= Lights - 2 ? Warning : Accent) : MeterOff;
            ink.Glowing(on ? 1 : 0).Box(new Vector2(meter.Min.X, bottom - step + 1.5f), new Vector2(meter.Max.X, bottom), color, 1);
        }

        ink.Glowing(0);
    }

    // The keys on the keybed; a held key sinks and warms to amber
    private void DrawKeys()
    {
        var held = rack.Notes.Held;
        _sounding.Clear();
        foreach (var playing in rack.Notes.Playing)
        {
            _sounding.Add(playing.Semitone);
        }

        foreach (var key in rackInput.Layout.PianoKeys)
        {
            var down = held.Contains(key.Semitone);
            var sounding = _sounding.Contains(key.Semitone);
            var top = _space.Keys.ToWorld(key.Bounds.Centre);
            var sink = down ? -0.004f : 0;
            if (key.IsBlack)
            {
                var centre = top + new Vector3(0, sink, 0);
                _blackKey.Add(Matrix4x4.CreateTranslation(centre), sounding ? Rgb(0xB8772A) : BlackKey, shine: 0.6f,
                    emissive: sounding ? Accent * 0.25f : default);
            }
            else
            {
                var centre = top + new Vector3(0, -0.011f + sink, 0);
                _whiteKey.Add(Matrix4x4.CreateTranslation(centre), sounding ? Accent : WhiteKey, shine: 0.45f,
                    emissive: sounding ? Accent * 0.2f : default);
            }
        }
    }

    // The keyboard's cheek: its wheels' slots, the octave's and the voices' readouts, and its outputs
    private void DrawCheekStill()
    {
        if (rackInput.Layout.Cheek is not { } cheek)
        {
            return;
        }

        var ink = _ink.On(_space.Cheek.ToMatrix(0.0004f));
        foreach (var wheel in cheek.Wheels)
        {
            var bounds = wheel.Bounds;
            ink.Box(bounds.Min, bounds.Max, JackHole, 6);
            ink.Box(bounds.Min - new Vector2(3), bounds.Max + new Vector2(3), KnobTrack, 8, outline: 1.6f);
        }

        foreach (var readout in (ReadOnlySpan<Box>)[cheek.Readout, cheek.VoicesReadout])
        {
            ink.Box(readout.Min - new Vector2(3), readout.Max + new Vector2(3), PanelEdge, 5);
            ink.Box(readout.Min, readout.Max, Plate, 3);
        }

        foreach (var knob in cheek.Knobs)
        {
            DrawKnobStill(knob);
        }

        foreach (var jack in cheek.Jacks)
        {
            DrawJackStill(jack, labelled: false);
        }
    }

    // The cheek's wheels rolling in their slots, the octave over its buttons, the voices, and its outputs labelled
    private Result DrawCheek()
    {
        if (rackInput.Layout.Cheek is not { } cheek)
        {
            return Result.Success();
        }

        var surface = _space.Cheek;
        var ink = _ink.On(surface.ToMatrix(0.0004f));
        foreach (var wheel in cheek.Wheels)
        {
            var bounds = wheel.Bounds;
            var value = rack.Patch.GetValue(wheel.Module, wheel.Parameter.Name);
            var turn = Knobs.ToTurn(wheel.Parameter, value) * 2 - 1;
            // The wheel's axle under the cheek's top, so only its top stands out of the slot
            var axle = surface.ToWorld(bounds.Centre) - Vector3.UnitY * (WheelRadius - 0.017f);
            _wheel.Add(Matrix4x4.CreateRotationX(-turn * WheelRoll) * Matrix4x4.CreateTranslation(axle), Rubber, shine: 0.12f);
            var dragged = rackInput.Drags.Any(d => d.Drag is RackDrag.Wheel w && w.Layout == wheel);
            var label = dragged ? Knobs.Format(wheel.Parameter, value) : wheel.Parameter.Caption;
            ink.TextCentred(new Vector2(bounds.Centre.X, bounds.Max.Y + 14), label, LabelDot, dragged ? Accent : Soft);
        }

        var octave = rack.Patch.GetValue(cheek.Keyboard.Id, Modules.Octave);
        ink.Glowing().TextCentred(cheek.Readout.Centre, $"OCT {octave:+0;-0;0}", 2.2f, Accent);
        ink.Glowing(0);
        foreach (var button in cheek.Buttons)
        {
            if (DrawButton(button, 0, button.Action == ButtonAction.OctaveUp ? "+" : "-").TryPickProblems(out var problems))
            {
                return problems;
            }
        }

        foreach (var knob in cheek.Knobs)
        {
            DrawKnob(knob);
        }

        var voices = rack.Notes.Voices;
        ink = _ink.On(surface.ToMatrix(0.0004f));
        ink.Glowing().TextCentred(cheek.VoicesReadout.Centre, voices == 1 ? "MONO" : $"POLY {voices}", 2, Accent);
        ink.Glowing(0);

        // Each output labelled beside it; an amber bracket, marked POLY, holds the ones carrying the keys' voices
        foreach (var jack in cheek.Jacks)
        {
            DrawJack(jack);
            ink = _ink.On(_space.SurfaceAt(jack.Centre).ToMatrix(InkLift));
            ink.TextCentred(jack.Centre + new Vector2(jack.Radius + 34, 0), jack.Definition.Name.ToUpperInvariant(), LabelDot, Ink);
        }

        var polyJacks = cheek.Jacks.Where(j => !j.Definition.Shared).ToList();
        if (voices > 1 && polyJacks.Count > 0)
        {
            var x = polyJacks[0].Centre.X + polyJacks[0].Radius + 56;
            var top = polyJacks.Min(j => j.Centre.Y) - 10;
            var bottom = polyJacks.Max(j => j.Centre.Y) + 10;
            ink = _ink.On(_space.SurfaceAt(polyJacks[0].Centre).ToMatrix(InkLift)).Glowing(0.5f);
            ink.Segment(new Vector2(x - 5, top), new Vector2(x, top), 1.4f, Accent);
            ink.Segment(new Vector2(x, top), new Vector2(x, bottom), 1.4f, Accent);
            ink.Segment(new Vector2(x - 5, bottom), new Vector2(x, bottom), 1.4f, Accent);
            // POLY reads down the bracket's outside
            var middle = new Vector2(x + 7, (top + bottom) / 2);
            var turned = Matrix4x4.CreateTranslation(-middle.X, -middle.Y, 0) * Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(middle.X, middle.Y, 0);
            _ink.On(turned * _space.SurfaceAt(polyJacks[0].Centre).ToMatrix(InkLift)).Glowing(0.5f).TextCentred(middle, "POLY", 1.2f, Accent);
        }

        return Result.Success();
    }

    private Result DrawCables()
    {
        // A cable carrying the keys' voices is thicker, as a bundle of them
        _cableShapes.Clear();
        foreach (var cable in rack.Patch.Cables)
        {
            if (_jackTops.TryGetValue(cable.From, out var from) && _jackTops.TryGetValue(cable.To, out var to))
            {
                _cableShapes.Add(new CableShape(new CableEnds(from.Top, from.Out, to.Top, to.Out), (int)rack.Patch.SignalOf(cable.From), rack.IsPolyphonic(cable) ? rack.Notes.Voices : 1, CableBow.Straight));
            }
        }

        // Cables sharing a jack, or running between the same places, fan out so each can be told apart
        var bows = CableRoutes.Fan([.. _cableShapes.Select(c => c.Ends)]);
        for (var i = 0; i < bows.Length; i++)
        {
            _cableShapes[i] = _cableShapes[i] with { Bow = bows[i] };
        }

        // A cable being dragged moves every frame, so it is drawn into the frame on its own
        _draggedShapes.Clear();
        foreach (var drag in _cableDrags)
        {
            var held = (RackDrag.Cable)drag.Drag;
            if (_jackTops.TryGetValue(held.Fixed, out var start) && float.IsFinite(drag.Position.X))
            {
                // The loose end hovers over the surface under the pointer
                var surface = _space.SurfaceAt(drag.Position);
                var poly = rack.IsPolyphonic(held.Fixed);
                _draggedShapes.Add(new CableShape(new CableEnds(start.Top, start.Out, surface.ToWorld(drag.Position) + surface.Normal * 0.03f, surface.Normal), (int)rack.Patch.SignalOf(held.Fixed), poly ? rack.Notes.Voices : 1, CableBow.Straight));
            }
        }

        if (_draggedShapes.Count > 0 && DrawCableShapes(_draggedShapes, _draggedCables).TryPickProblems(out var problems))
        {
            return problems;
        }

        // The patch's tubes and plugs are drawn again only when one of its cables changes
        if (_cableShapes.SequenceEqual(_drawnCables))
        {
            return Result.Success();
        }

        _drawnCables.Clear();
        _drawnCables.AddRange(_cableShapes);
        return _layers.Draw(CableLayer, () => DrawCableShapes(_drawnCables, _cables));
    }

    // The cables' plugs, then their tubes, one mesh per colour they have, made the first time a colour is drawn
    private Result DrawCableShapes(List<CableShape> shapes, Dictionary<int, (MeshId<Shaders.Shared.LitVertex> Mesh, Batch Batch)> meshes)
    {
        foreach (var ((from, fromOut, to, toOut), color, voices, _) in shapes)
        {
            var poly = voices > 1;
            var dark = (CableColors[color] * 0.55f) with { W = 1 };
            // Each plug a knurled metal barrel and a rubber boot in the cable's colour; a polyphonic cable's plugs are
            // stouter, with a bead on the cable just past each showing how many voices it carries
            var girth = Matrix4x4.CreateScale(poly ? PolyPlug : 1, 1, poly ? PolyPlug : 1);
            foreach (var (at, outward) in new[] { (from, fromOut), (to, toOut) })
            {
                var frame = Frame(PerpendicularTo(outward), outward, Vector3.Cross(PerpendicularTo(outward), outward), at);
                _plug.Add(girth * frame, JackMetal, shine: 0.6f, finish: BareMetal);
                _boot.Add(girth * frame, dark, shine: 0.2f, finish: new Vector4(0, 0, 0.004f, 0));
                if (poly)
                {
                    DrawBead(at + outward * 0.034f, voices);
                }
            }
        }

        for (var color = 0; color < CableColors.Length; color++)
        {
            // A colour with no cables draws no instance, so its mesh, as it was, shows nothing
            if (!shapes.Exists(c => c.Color == color))
            {
                continue;
            }

            var paths = shapes.Where(c => c.Color == color).Select(c => (CableRoutes.Path(c.Ends, c.Bow), c.Poly ? CableRadius * PolyGirth : CableRadius));
            if (Tubes(paths, capped: true).TryPickProblems(out var problems, out var buffers))
            {
                return problems;
            }

            if (!meshes.TryGetValue(color, out var mesh))
            {
                if (RegisterMesh(buffers, glows: true).TryPickProblems(out problems, out var registered))
                {
                    return problems;
                }

                meshes[color] = mesh = (registered.Mesh, new Batch(registered.Batch));
            }
            else if (meshManager.Update(mesh.Mesh, buffers).TryPickProblems(out problems))
            {
                return problems;
            }

            mesh.Batch.Add(Matrix4x4.Identity, CableColors[color], shine: 0.6f, finish: CableSheen);
        }

        return Result.Success();
    }

    // A dark bead on a polyphonic cable with the voice count on it in amber, turned to the eye
    private void DrawBead(Vector3 at, int voices)
    {
        _bead.Add(Matrix4x4.CreateTranslation(at), JackHole, shine: 0.7f, finish: new Vector4(0, 0.3f, 0, 0));
        var eye = seat.Seat.Camera.View.Position;
        var normal = Vector3.Normalize(eye - at);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, normal));
        var down = Vector3.Cross(right, normal);
        var face = new Surface(at + normal * 0.0052f, right * RackSpace.Scale, down * RackSpace.Scale, normal);
        _ink.On(face.ToMatrix()).Glowing(0.7f).TextCentred(Vector2.Zero, $"{voices}", 0.85f, Accent);
    }

    // Where a plug sits on a jack, and which way it comes out: off the panels, or off a scope's bezel
    private (Vector3 Top, Vector3 Out) JackTop(Vector2 design)
    {
        var surface = _space.SurfaceAt(design);
        var lift = surface == _space.Panels ? PanelThickness + 0.0045f : 0.0045f;
        return (surface.ToWorld(design) + surface.Normal * lift, surface.Normal);
    }

    private static Vector3 PerpendicularTo(Vector3 v) =>
        Vector3.Normalize(Vector3.Cross(MathF.Abs(v.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX, v));

    /// <summary>How much of the screen's width the hint line may take: the middle, clear of the scopes, or nearly all of it where they stand lower, over the case.</summary>
    private float HintWidth => _space.Portrait ? 0.92f : 0.5f;

    private Result DrawHint()
    {
        var hint = rackInput.Hint;
        var text = hint.Text;
        var color = hint.Tone switch
        {
            HintTone.Problem => Warning,
            HintTone.Start => Accent,
            _ => Soft,
        };
        var size = window.ContentAreaSize;
        var screen = new Vector2(int.Max(1, size.X), int.Max(1, size.Y));
        // Between the scopes, which stand at the top corners
        var dot = MathF.Min(screen.Y / 420, screen.X * HintWidth / (DotFont.Width(text) + 8));
        overlay.Begin();
        overlay.TextCentred(new Vector2(screen.X / 2, dot * 8), text, dot, color);
        DrawTurnArrow();
        return overlay.End(screen);
    }

    // The arrow on the screen's edge that turns the seat: a dark tab with a chevron pointing where it turns and the name of
    // what is there, filling up from the bottom as the mouse rests on it
    private void DrawTurnArrow()
    {
        if (seat.Arrow is not { } arrow)
        {
            return;
        }

        var turned = seat.Seat.Turned;
        var hovered = seat.Dwell > 0;
        overlay.Box(arrow.Min, arrow.Max, Plate with { W = 0.78f }, 8);
        overlay.Box(arrow.Min, arrow.Max, (hovered ? Accent : PanelEdge) with { W = 0.9f }, 8, outline: 1.5f);
        if (seat.Dwell > 0)
        {
            overlay.Box(new Vector2(arrow.Min.X, arrow.Max.Y - arrow.Size.Y * seat.Dwell), arrow.Max, Accent with { W = 0.18f }, 8);
        }

        // The chevron, pointing left to the side case or right back to the rack
        var centre = arrow.Centre - new Vector2(0, arrow.Size.Y * 0.1f);
        var reach = arrow.Size.X * 0.2f;
        var point = centre + new Vector2(turned ? reach : -reach, 0);
        var stroke = arrow.Size.X * 0.09f;
        overlay.Segment(point, centre + new Vector2(turned ? -reach : reach, -reach * 1.6f), stroke, Ink);
        overlay.Segment(point, centre + new Vector2(turned ? -reach : reach, reach * 1.6f), stroke, Ink);
        var label = turned ? "RACK" : "SEQ";
        var dot = MathF.Min(arrow.Size.X * 0.8f / DotFont.Width(label), arrow.Size.Y * 0.05f);
        overlay.TextCentred(new Vector2(arrow.Centre.X, arrow.Max.Y - arrow.Size.Y * 0.16f), label, dot, hovered ? Accent : Soft);
    }

    // The frame of a part standing out of the panels at a design point: x right, y out of the panel, z down
    private Matrix4x4 OnPanel(Vector2 design)
    {
        var panels = _space.SurfaceAt(design);
        return Frame(Vector3.Normalize(panels.Right), panels.Normal, Vector3.Normalize(panels.Down), panels.ToWorld(design) + panels.Normal * PanelThickness);
    }

    /// <summary>A world matrix from a local frame: the axes as rows, then the origin.</summary>
    private static Matrix4x4 Frame(Vector3 x, Vector3 y, Vector3 z, Vector3 origin) => new(
        x.X, x.Y, x.Z, 0,
        y.X, y.Y, y.Z, 0,
        z.X, z.Y, z.Z, 0,
        origin.X, origin.Y, origin.Z, 1);

    private static Vector4 Rgb(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);
}
