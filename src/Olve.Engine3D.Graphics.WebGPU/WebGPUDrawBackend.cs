using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;
using EngineVertexBufferLayout = Olve.Engine3D.Rendering.VertexBufferLayout;
using WgpuVertexBufferLayout = Olve.WebGPU.VertexBufferLayout;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Draws as one render pipeline each, for their pass's attachments, with their inputs; the frame binds them with the pass's inputs.</summary>
internal sealed class WebGPUDrawBackend(Provider<WebGPUContext> contextProvider, WebGPUScreen screen, WebGPUResources resources)
    : IDrawBackend
{
    public Result CreateDraw<TInputs>(
        DrawId<TInputs> drawId,
        PassId passId,
        ShaderId shaderId,
        GroupId groupId,
        RenderState renderState,
        TInputs inputs)
        where TInputs : IGroupInputs
    {
        if (resources.Draws.ContainsKey(drawId))
        {
            return new ResultProblem("Draw '{0}' already exists", drawId);
        }

        if (!resources.Passes.TryGetValue(passId, out var pass))
        {
            return new ResultProblem("Pass '{0}' has no WebGPU state", passId);
        }

        if (!resources.Shaders.TryGetValue(shaderId, out var shader))
        {
            return new ResultProblem("Shader '{0}' has no WebGPU modules", shaderId);
        }

        if (!resources.Groups.TryGetValue(groupId, out var group))
        {
            return new ResultProblem("Group '{0}' has no WebGPU state", groupId);
        }

        var context = contextProvider.Value;
        if (CreatePipeline(context, shader, group, renderState, pass).TryPickProblems(out var problems, out var pipeline))
        {
            return problems.Prepend("Failed to create the pipeline of draw '{0}'", drawId);
        }

        resources.Draws[drawId] = new WgpuDraw(pipeline, passId, shaderId, groupId, WgpuInputs.Create(context, inputs));
        return Result.Success();
    }

    public Result SetInputs<TInputs>(DrawId<TInputs> drawId, TInputs inputs)
        where TInputs : IGroupInputs
    {
        if (!resources.Draws.TryGetValue(drawId, out var draw))
        {
            return new ResultProblem("Draw '{0}' has no WebGPU pipeline", drawId);
        }

        draw.Inputs.Set(inputs);
        return Result.Success();
    }

    public DeletionResult DeleteDraw(DrawId drawId)
    {
        if (!resources.Draws.Remove(drawId, out var draw))
        {
            return DeletionResult.NotFound();
        }

        draw.Release();
        return DeletionResult.Success();
    }

    private Result<RenderPipeline> CreatePipeline(
        WebGPUContext context, WgpuShader shader, WgpuGroup group, RenderState renderState, WgpuPass pass)
    {
        // Passes into the screen draw with its samples, and passes into a multisampled texture with the texture's
        var target = pass.Color?.Texture ?? pass.Depth?.Texture;
        var sampleCount = target == TextureManager.Screen
            ? screen.SampleCount
            : target is not null && resources.Textures.TryGetValue(target, out var texture) ? texture.SampleCount : 1u;

        // The mesh's vertices and the group's instances take the first slots, skipping those without floats
        WgpuVertexBufferLayout[] bufferLayouts =
        [
            .. new[] { group.VertexLayout, group.InstanceLayout }
                .Where(layout => layout.Attributes.Count > 0)
                .Select(ToVertexBufferLayout),
        ];

        // The pass's attachments: a depth-only pass has no colour target, and a pass without depth tests none
        ColorTargetState[] colorTargets = pass.Color is { } color
            ?
            [
                new ColorTargetState
                {
                    Format = color.Format,
                    Blend = renderState.Blend == BlendMode.None ? null : ToBlendState(renderState.Blend),
                    WriteMask = ColorWriteMask.All,
                },
            ]
            : [];

        DepthStencilState? depthStencil = pass.Depth is null
            ? null
            : new DepthStencilState
            {
                Format = TextureFormat.Depth32Float,
                DepthWriteEnabled = renderState.DepthWrite ? OptionalBool.True : OptionalBool.False,
                DepthCompare = renderState.DepthTest ? CompareFunction.Less : CompareFunction.Always,
            };

        // No layout: WebGPU derives bind group 0 from the bindings the shader reads, which the frame fills from the inputs
        var descriptor = new RenderPipelineDescriptor
        {
            Vertex = new VertexState
            {
                Module = shader.VertexModule,
                EntryPoint = shader.VertexEntryPoint,
                Buffers = bufferLayouts,
            },
            Primitive = new PrimitiveState
            {
                Topology = ToPrimitiveTopology(renderState.Topology),
                FrontFace = FrontFace.CCW,
                CullMode = CullMode.None,
            },
            DepthStencil = depthStencil,
            Multisample = new MultisampleState { Count = sampleCount },
            Fragment = new FragmentState
            {
                Module = shader.FragmentModule,
                EntryPoint = shader.FragmentEntryPoint,
                Targets = colorTargets,
            },
        };

        context.PushErrorScope();
        var pipeline = context.Device.CreateRenderPipeline(descriptor);
        if (context.PopErrorScope("creating a render pipeline").TryPickProblems(out var problems))
        {
            pipeline.Dispose();
            return problems;
        }

        return pipeline;
    }

    private static WgpuVertexBufferLayout ToVertexBufferLayout(EngineVertexBufferLayout layout) => new()
    {
        ArrayStride = (ulong)layout.StrideBytes,
        StepMode = layout.Instanced ? VertexStepMode.Instance : VertexStepMode.Vertex,
        Attributes = layout.Attributes
            .Select(attribute => new VertexAttribute
            {
                Format = ToVertexFormat(attribute.ComponentCount),
                Offset = (ulong)attribute.ByteOffset,
                ShaderLocation = attribute.Location,
            })
            .ToArray(),
    };

    private static VertexFormat ToVertexFormat(int componentCount) => componentCount switch
    {
        1 => VertexFormat.Float32,
        2 => VertexFormat.Float32x2,
        3 => VertexFormat.Float32x3,
        _ => VertexFormat.Float32x4,
    };

    // Source alpha over one minus source alpha, blending alpha the same way as colour
    private static BlendState ToBlendState(BlendMode blend)
    {
        var component = blend == BlendMode.Alpha
            ? new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.SrcAlpha, DstFactor = BlendFactor.OneMinusSrcAlpha }
            : new BlendComponent { Operation = BlendOperation.Add, SrcFactor = BlendFactor.One, DstFactor = BlendFactor.Zero };
        return new BlendState { Color = component, Alpha = component };
    }

    private static PrimitiveTopology ToPrimitiveTopology(Topology topology) => topology switch
    {
        Topology.LineList => PrimitiveTopology.LineList,
        Topology.LineStrip => PrimitiveTopology.LineStrip,
        _ => PrimitiveTopology.TriangleList,
    };
}
