using System.Numerics;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Generated.Shaders;
using Olve.Utilities.IntegerMath2D;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// The room's finish: the room is drawn into <see cref="Scene"/>, a multisampled texture the screen's size, and laid on
/// the screen through it. The bloom: the room's draws that can glow (and those that can hide them) are drawn again,
/// small and multisampled, into a glow texture where each surface gives only the light it emits (<c>glowOnly</c> in the room pass), blurred across and then down, and laid over
/// the room, both read through a cubic B-spline so the small texture's texels don't show as blocks, with a little of the glow itself for a tight core, so lit things glow in their own colours and matte ones
/// don't. Then a soft vignette, and a colour fringe at the far edges of the room as the output gets loud. Each part can
/// be turned off (<see cref="FinishOptions"/>). The textures follow the screen's size.
/// </summary>
public sealed class RoomFinish(
    RenderingSegment segment,
    PassManager passManager,
    ShaderManager shaderManager,
    MeshManager meshManager,
    GroupManager groupManager,
    DrawManager drawManager,
    TextureManager textureManager,
    SamplerManager samplerManager,
    IWindowInfo window,
    FinishOptions options)
{
    /// <summary>How much smaller than the screen the glow is drawn, each way.</summary>
    private const int GlowDivisor = 4;

    /// <summary>The blurred glow's strength, the core's, how dark the vignette makes the corners, and the most the colours fringe apart.</summary>
    private const float BlurStrength = 1.15f, CoreStrength = 0.45f, Vignette = 0.38f, MaxFringe = 0.006f;

    private PassId<IColorFormat<ITextureFormat.Rgba8Unorm>, Shaders.Shared.SpacePass>? _glow;

    /// <summary>What the room is drawn into, multisampled, and its depth buffer; set by <see cref="Load"/>.</summary>
    public TextureId<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget> Scene { get; private set; } = null!;

    public TextureId<ITextureFormat.Depth, ITextureUsage.RenderTarget> SceneDepth { get; private set; } = null!;
    private InstanceBatch<IVertexData.None, Shaders.Bloom.Instance>? _finish;

    // The blurs, across and then down. Each steps in the glow's texel heights, whatever size the glow is now: a texel
    // down, and across as far on the screen, so the glow spreads evenly however the glow's rounded shape differs from
    // the screen's
    private readonly List<(InstanceBatch<IVertexData.None, Shaders.Blur.Instance> Batch, bool Across)> _blurs = [];

    public FinishOptions Options => options;

    public Result Load()
    {
        var black = new ITextureFormat.Rgba8Unorm(0, 0, 0, 0);
        if (textureManager.CreateScreenSized<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>(1, segment, multisampled: true)
                .TryPickProblems(out var problems, out var scene)
            || textureManager.CreateScreenSized<ITextureFormat.Depth, ITextureUsage.RenderTarget>(1, segment, multisampled: true)
                .TryPickProblems(out problems, out var sceneDepth)
            || Glow<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>(multisampled: true).TryPickProblems(out problems, out var glow)
            || Glow<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>().TryPickProblems(out problems, out var across)
            || Glow<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>().TryPickProblems(out problems, out var blurred)
            || Glow<ITextureFormat.Depth, ITextureUsage.RenderTarget>(multisampled: true).TryPickProblems(out problems, out var depth)
            || samplerManager.Register<ISamplerKind.Filtering>(new SamplerDescription(TextureFilter.Linear, TextureWrap.ClampToEdge), segment)
                .TryPickProblems(out problems, out var sampler)
            || shaderManager.Register(Shaders.Blur.Definition, segment).TryPickProblems(out problems, out var blurShader)
            || shaderManager.Register(Shaders.Bloom.Definition, segment).TryPickProblems(out problems, out var bloomShader)
            || meshManager.Register(new MeshBuffers<IVertexData.None> { Vertices = [default, default, default] }, segment)
                .TryPickProblems(out problems, out var triangle))
        {
            return problems.Prepend("Failed to set up the room's finish");
        }

        Scene = scene;
        SceneDepth = sceneDepth;
        if (options.Bloom)
        {
            // After the room, before the blur
            if (passManager.Register(ColorAttachment.Clear(glow, black), DepthAttachment.Clear(depth, 1f), new Shaders.Shared.SpacePass(), priority: 10, segment)
                .TryPickProblems(out problems, out var glowPass))
            {
                return problems.Prepend("Failed to set up the glow pass");
            }

            _glow = glowPass;
            (TextureId<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget> From, TextureId<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget> To, bool Across, int Priority)[] blurs =
            [
                (glow, across, true, 11),
                (across, blurred, false, 12),
            ];
            foreach (var (from, to, isAcross, priority) in blurs)
            {
                if (passManager.Register(ColorAttachment.Clear(to, black), new IPassInputs.None(), priority, segment).TryPickProblems(out problems, out var blurPass)
                    || groupManager.Register<IVertexData.None, Shaders.Blur.Instance>(triangle, segment).TryPickProblems(out problems, out var blurGroup)
                    || drawManager.Register(blurPass, blurShader, blurGroup, new Shaders.Blur.Inputs { USource = TextureSampler.Of(from, sampler) },
                        new RenderState(BlendMode.None, DepthWrite: false, DepthTest: false), key: 0, segment).TryPickProblems(out problems, out _))
                {
                    return problems.Prepend("Failed to set up the bloom's blur");
                }

                _blurs.Add((new InstanceBatch<IVertexData.None, Shaders.Blur.Instance>(groupManager, blurGroup, new Shaders.Blur.Instance(Vector4.Zero)), isAcross));
            }
        }

        // The room onto the screen, under the hint line
        var onScreen = ColorAttachment.Of(TextureManager.Screen, new Load<ITextureFormat.Rgba8Unorm>.Clear(black));
        if (passManager.Register(onScreen, new IPassInputs.None(), priority: 50, segment).TryPickProblems(out problems, out var finishPass)
            || groupManager.Register<IVertexData.None, Shaders.Bloom.Instance>(triangle, segment).TryPickProblems(out problems, out var finishGroup)
            || drawManager.Register(finishPass, bloomShader, finishGroup,
                new Shaders.Bloom.Inputs { UScene = TextureSampler.Of(scene, sampler), UGlow = TextureSampler.Of(glow, sampler), UBlur = TextureSampler.Of(blurred, sampler) },
                new RenderState(BlendMode.None, DepthWrite: false, DepthTest: false), key: 0, segment).TryPickProblems(out problems, out _))
        {
            return problems.Prepend("Failed to set up the room's finish");
        }

        _finish = new InstanceBatch<IVertexData.None, Shaders.Bloom.Instance>(groupManager, finishGroup, new Shaders.Bloom.Instance(Vector4.Zero));
        return Result.Success();
    }

    // A glow texture: a quarter of the screen each way, following it; without the bloom only there to be read, a pixel.
    // The glow pass's are multisampled as the room is, so a light smaller than a glow texel still gives its share of light
    // instead of flickering in and out, and its edges don't step a texel at a time
    private Result<TextureId<TFormat, TUsage>> Glow<TFormat, TUsage>(bool multisampled = false)
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage.IRenderable =>
        options.Bloom
            ? textureManager.CreateScreenSized<TFormat, TUsage>(GlowDivisor, segment, multisampled)
            : textureManager.Create<TFormat, TUsage>(new Size(1, 1), segment);

    /// <summary>Draws a draw of the room into the glow too, with the same group, inputs and state; nothing without the bloom.</summary>
    public Result AlsoGlows<TVertex, TInstance, TInputs>(
        ShaderId<TVertex, TInstance, IColorFormat<ITextureFormat.Rgba8Unorm>, Shaders.Shared.SpacePass, TInputs> shader,
        GroupId<TVertex, TInstance> group,
        TInputs inputs,
        RenderState state,
        int key)
        where TVertex : IVertexData
        where TInstance : struct, IInstanceData
        where TInputs : IGroupInputs =>
        _glow is { } glow && drawManager.Register(glow, shader, group, inputs, state, key, segment).TryPickProblems(out var problems, out _)
            ? problems.Prepend("Failed to draw into the glow")
            : Result.Success();

    /// <summary>Sets the frame's glow pass from the room's, and the fringe by how loud the output is (smoothed, from 0 to 1).</summary>
    public Result Render(Shaders.Shared.SpacePass room, float loudness)
    {
        if (_glow is { } glow && passManager.SetInputs(glow, room with { GlowOnly = Vector4.UnitX }).TryPickProblems(out var problems))
        {
            return problems;
        }

        var screen = window.ContentAreaSize;
        var aspect = screen.Y > 0 ? screen.X / (float)screen.Y : 1;
        foreach (var (blur, isAcross) in _blurs)
        {
            blur.Begin();
            blur.Add(new Shaders.Blur.Instance(new Vector4(isAcross ? new Vector2(1 / aspect, 0) : Vector2.UnitY, 0, 0)));
            if (blur.End().TryPickProblems(out var blurProblems))
            {
                return blurProblems;
            }
        }

        if (_finish is not { } finish)
        {
            return Result.Success();
        }

        finish.Begin();
        finish.Add(new Shaders.Bloom.Instance(new Vector4(
            options.Bloom ? BlurStrength : 0,
            options.Bloom ? CoreStrength : 0,
            options.Vignette ? Vignette : 0,
            options.Aberration ? MaxFringe * float.Clamp(loudness, 0, 1) : 0)));
        return finish.End();
    }
}

/// <summary>Which parts of the room's finish are drawn: the page turns them off with <c>?bloom=0</c>, <c>?vignette=0</c> or <c>?fringe=0</c>.</summary>
public sealed record FinishOptions(bool Bloom = true, bool Vignette = true, bool Aberration = true);
