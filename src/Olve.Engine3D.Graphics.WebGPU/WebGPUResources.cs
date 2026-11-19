using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Utilities.IntegerMath2D;
using Olve.WebGPU;
using EngineVertexBufferLayout = Olve.Engine3D.Rendering.VertexBufferLayout;
using WgpuBuffer = Olve.WebGPU.Buffer;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>The WebGPU objects of one scene scope, by the engine id they were created for.</summary>
internal sealed class WebGPUResources(ILogger<WebGPUResources> logger) : IDisposable
{
    public Dictionary<TextureId, WgpuTexture> Textures { get; } = [];
    public Dictionary<SamplerId, Sampler> Samplers { get; } = [];
    public Dictionary<MeshId, WgpuMesh> Meshes { get; } = [];
    public Dictionary<GroupId, WgpuGroup> Groups { get; } = [];
    public Dictionary<ShaderId, WgpuShader> Shaders { get; } = [];
    public Dictionary<PassId, WgpuPass> Passes { get; } = [];
    public Dictionary<DrawId, WgpuDraw> Draws { get; } = [];

    public void Dispose()
    {
        var leftovers = Textures.Count + Samplers.Count + Meshes.Count + Groups.Count + Shaders.Count + Passes.Count + Draws.Count;
        if (leftovers == 0)
        {
            return;
        }

        logger.LogWarning("{Count} WebGPU object(s) were not deleted by their managers; deleting them now", leftovers);

        foreach (var draw in Draws.Values)
        {
            draw.Release();
        }

        foreach (var pass in Passes.Values)
        {
            pass.Release();
        }

        foreach (var shader in Shaders.Values)
        {
            shader.Release();
        }

        foreach (var group in Groups.Values)
        {
            group.Release();
        }

        foreach (var mesh in Meshes.Values)
        {
            mesh.Release();
        }

        foreach (var sampler in Samplers.Values)
        {
            sampler.Dispose();
        }

        foreach (var texture in Textures.Values)
        {
            texture.Release();
        }

        Draws.Clear();
        Passes.Clear();
        Shaders.Clear();
        Groups.Clear();
        Meshes.Clear();
        Samplers.Clear();
        Textures.Clear();
    }
}

/// <summary>
/// A texture with one mip level, the view passes and shaders use, its size and format. A multisampled one passes draw
/// into with <see cref="SampleCount"/> samples per pixel: into <see cref="Samples"/>, resolved into the texture, when it
/// is read by shaders; else the texture is the samples. <see cref="Generation"/> counts the times it was made again at a
/// new size, so what binds it knows to bind it afresh.
/// </summary>
internal sealed class WgpuTexture(
    Texture texture,
    TextureView view,
    Size size,
    WebGPUTextureFormats.Formats formats,
    uint sampleCount = 1,
    WgpuSamples? samples = null,
    int generation = 0)
{
    public Texture Texture { get; } = texture;
    public TextureView View { get; } = view;
    public Size Size { get; } = size;
    public WebGPUTextureFormats.Formats Formats { get; } = formats;
    public uint SampleCount { get; } = sampleCount;
    public WgpuSamples? Samples { get; } = samples;
    public int Generation { get; } = generation;

    public void Release()
    {
        View.Dispose();
        Texture.Dispose();
        Samples?.Release();
    }
}

/// <summary>The samples a multisampled texture is drawn into before they are resolved into it.</summary>
internal sealed record WgpuSamples(Texture Texture, TextureView View)
{
    public void Release()
    {
        View.Dispose();
        Texture.Dispose();
    }
}

/// <summary>A mesh's buffers: a vertex buffer unless its vertices have no floats, and an index buffer if it is indexed.</summary>
internal sealed class WgpuMesh
{
    public WgpuBuffer VertexBuffer { get; set; }
    public ulong VertexBufferSize { get; set; }
    public uint VertexCount { get; set; }
    public WgpuBuffer IndexBuffer { get; set; }
    public ulong IndexBufferSize { get; set; }
    public uint IndexCount { get; set; }

    public void Release()
    {
        VertexBuffer.Dispose();
        VertexBuffer = default;
        IndexBuffer.Dispose();
        IndexBuffer = default;
    }
}

/// <summary>A group's mesh and layouts, and its instances packed in one float array; the slots that changed are uploaded before the group is drawn.</summary>
internal sealed class WgpuGroup(MeshId mesh, EngineVertexBufferLayout vertexLayout, EngineVertexBufferLayout instanceLayout, int floatsPerInstance)
{
    private readonly Dictionary<InstanceId, int> _slots = [];
    private readonly List<InstanceId> _ids = [];
    private float[] _floats = [];
    private ulong _bufferSize;
    // The slots changed since the last upload, from the first to past the last; none when the first is past the last
    private int _dirtyFrom = int.MaxValue, _dirtyTo;

    public MeshId Mesh { get; } = mesh;
    public EngineVertexBufferLayout VertexLayout { get; } = vertexLayout;
    public EngineVertexBufferLayout InstanceLayout { get; } = instanceLayout;
    public int InstanceCount => _ids.Count;

    // Default until the group has instances with floats to upload
    public WgpuBuffer InstanceBuffer { get; private set; }

    public Result Add<TInstance>(InstanceId instanceId, TInstance instance) where TInstance : IInstanceData
    {
        if (_slots.ContainsKey(instanceId))
        {
            return new ResultProblem("Instance '{0}' is already in the group", instanceId);
        }

        var slot = _ids.Count;
        var needed = (slot + 1) * floatsPerInstance;
        if (_floats.Length < needed)
        {
            Array.Resize(ref _floats, System.Math.Max(needed, _floats.Length * 2));
        }

        _slots[instanceId] = slot;
        _ids.Add(instanceId);
        instance.WriteTo(GetFloats(slot));
        MarkDirty(slot);
        return Result.Success();
    }

    public Result Update<TInstance>(InstanceId instanceId, TInstance instance) where TInstance : IInstanceData
    {
        if (!_slots.TryGetValue(instanceId, out var slot))
        {
            return new ResultProblem("Instance '{0}' is not in the group", instanceId);
        }

        instance.WriteTo(GetFloats(slot));
        MarkDirty(slot);
        return Result.Success();
    }

    public bool Remove(InstanceId instanceId)
    {
        if (!_slots.Remove(instanceId, out var slot))
        {
            return false;
        }

        var last = _ids.Count - 1;
        if (slot != last)
        {
            GetFloats(last).CopyTo(GetFloats(slot));
            _ids[slot] = _ids[last];
            _slots[_ids[slot]] = slot;
        }

        _ids.RemoveAt(last);
        MarkDirty(slot);
        return true;
    }

    // Replaces the instance buffer with a larger one when the instances no longer fit, uploading them all into it
    public void UploadIfDirty(WebGPUContext context)
    {
        var size = (ulong)(InstanceCount * floatsPerInstance * sizeof(float));
        var to = int.Min(_dirtyTo, InstanceCount);
        if (_dirtyFrom >= to || size == 0)
        {
            ClearDirty();
            return;
        }

        var from = _dirtyFrom;
        if (size > _bufferSize)
        {
            // Frames already submitted keep the old buffer alive until they finish
            InstanceBuffer.Dispose();

            _bufferSize = (ulong)(_floats.Length * sizeof(float));
            InstanceBuffer = context.CreateBuffer(BufferUsage.Vertex | BufferUsage.CopyDst, _bufferSize);
            (from, to) = (0, InstanceCount);
        }

        context.Queue.WriteBuffer(InstanceBuffer, (ulong)(from * floatsPerInstance * sizeof(float)),
            _floats.AsSpan(from * floatsPerInstance, (to - from) * floatsPerInstance));
        ClearDirty();
    }

    private void MarkDirty(int slot)
    {
        _dirtyFrom = int.Min(_dirtyFrom, slot);
        _dirtyTo = int.Max(_dirtyTo, slot + 1);
    }

    private void ClearDirty() => (_dirtyFrom, _dirtyTo) = (int.MaxValue, 0);

    public void Release()
    {
        InstanceBuffer.Dispose();
        InstanceBuffer = default;
    }

    private Span<float> GetFloats(int slot) => _floats.AsSpan(slot * floatsPerInstance, floatsPerInstance);
}

/// <summary>A shader's vertex and fragment modules, their entry points, where its pass and group inputs are bound, and which bindings it reads.</summary>
internal sealed class WgpuShader(
    ShaderModule vertexModule,
    byte[] vertexEntryPoint,
    ShaderModule fragmentModule,
    byte[] fragmentEntryPoint,
    WgslInputsBindings passBindings,
    WgslInputsBindings groupBindings,
    IReadOnlySet<int> readBindings)
{
    public IReadOnlySet<int> ReadBindings { get; } = readBindings;
    public ShaderModule VertexModule { get; } = vertexModule;
    public byte[] VertexEntryPoint { get; } = vertexEntryPoint;
    public ShaderModule FragmentModule { get; } = fragmentModule;
    public byte[] FragmentEntryPoint { get; } = fragmentEntryPoint;
    public WgslInputsBindings PassBindings { get; } = passBindings;
    public WgslInputsBindings GroupBindings { get; } = groupBindings;

    public void Release()
    {
        VertexModule.Dispose();
        FragmentModule.Dispose();
    }
}

/// <summary>A pass's or draw's inputs: a uniform buffer uploaded when its values change, and textures.</summary>
internal sealed class WgpuInputs
{
    // WGSL rounds a uniform struct's size up to 16 bytes, and the buffer must hold all of it
    private const int UniformAlignment = 16;

    private readonly float[] _values;
    private readonly float[] _scratch;
    private bool _isDirty = true;

    private WgpuInputs(WgpuBuffer buffer, int floatCount, int textureCount)
    {
        Buffer = buffer;
        _values = new float[floatCount];
        _scratch = new float[floatCount];
        Textures = new TextureBinding[textureCount];
    }

    // Default when the inputs have no values
    public WgpuBuffer Buffer { get; }
    public TextureBinding[] Textures { get; }

    public static WgpuInputs Create<TInputs>(WebGPUContext context, TInputs inputs) where TInputs : IInputs
    {
        var size = (TInputs.FloatCount * sizeof(float) + UniformAlignment - 1) / UniformAlignment * UniformAlignment;
        var buffer = size > 0 ? context.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, (ulong)size) : default;
        var wgpuInputs = new WgpuInputs(buffer, TInputs.FloatCount, TInputs.TextureCount);
        wgpuInputs.Set(inputs);
        return wgpuInputs;
    }

    public void Set<TInputs>(TInputs inputs) where TInputs : IInputs
    {
        inputs.WriteTextures(Textures);
        inputs.WriteTo(_scratch);

        if (MemoryMarshal.Cast<float, int>(_scratch).SequenceEqual(MemoryMarshal.Cast<float, int>(_values)))
        {
            return;
        }

        _scratch.CopyTo(_values, 0);
        _isDirty = true;
    }

    // Queued before the frame's commands, so it must happen before the frame is submitted
    public void UploadIfDirty(WebGPUContext context)
    {
        if (!_isDirty || Buffer == default)
        {
            _isDirty = false;
            return;
        }

        context.Queue.WriteBuffer(Buffer, 0, _values.AsSpan());
        _isDirty = false;
    }

    public void Release() => Buffer.Dispose();
}

/// <summary>A pass: its colour and depth attachments (null for none) and its inputs.</summary>
internal sealed class WgpuPass(PassColor? color, PassDepth? depth, WgpuInputs inputs)
{
    public PassColor? Color { get; } = color;
    public PassDepth? Depth { get; } = depth;
    public WgpuInputs Inputs { get; } = inputs;

    public void Release() => Inputs.Release();
}

/// <summary>The texture a pass draws colour into (or the screen), its format, and its clear colour, or null to keep what is there.</summary>
internal sealed record PassColor(TextureId Texture, TextureFormat Format, Color? Clear);

/// <summary>The texture a pass tests depth in, or null for the screen's depth buffer, and its clear depth, or null to keep it.</summary>
internal sealed record PassDepth(TextureId? Texture, float? Clear);

/// <summary>A draw: its pipeline, what it draws, its inputs, and the bind group last built for them.</summary>
internal sealed class WgpuDraw(RenderPipeline pipeline, PassId pass, ShaderId shader, GroupId group, WgpuInputs inputs)
{
    public RenderPipeline Pipeline { get; } = pipeline;
    public PassId Pass { get; } = pass;
    public ShaderId Shader { get; } = shader;
    public GroupId Group { get; } = group;
    public WgpuInputs Inputs { get; } = inputs;

    // Default until the frame builds one; rebuilt when the objects it binds change
    public BindGroup BindGroup { get; private set; }
    public BoundObject[] BoundObjects { get; private set; } = [];

    public void SetBindGroup(BindGroup bindGroup, BoundObject[] boundObjects)
    {
        BindGroup.Dispose();
        BindGroup = bindGroup;
        BoundObjects = boundObjects;
    }

    public void Release()
    {
        BindGroup.Dispose();
        Pipeline.Dispose();
        Inputs.Release();
    }
}
