using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Results;
using Olve.Utilities.IntegerMath2D;

namespace Olve.Engine3D.Tests.Rendering;

/// <summary>
/// Every rendering backend at once, keeping nothing: it records the frames it is given, and the WebGPU backends only report
/// deleted or not found, while the interfaces allow a failure, which the kind named <c>failing</c> returns.
/// </summary>
internal sealed class FakeRenderingBackend(string? failing = null)
    : ITextureBackend, IPassBackend, IShaderBackend, IMeshBackend, IGroupBackend, IDrawBackend, IFrameBackend
{
    public readonly record struct NoInstance : IInstanceData
    {
        public static int FloatCount => 0;
        public static VertexBufferLayout Layout { get; } = new(0, true, []);

        public void WriteTo(Span<float> buffer)
        {
        }
    }

    public sealed class Shader : IShaderDefinition<IVertexData.None, NoInstance, IColorFormat<ITextureFormat.Rgba8Unorm>, IPassInputs.None, IGroupInputs.None>
    {
        public ShaderData ShaderData { get; } = new()
        {
            Name = "test",
            Wgsl = new WgslShaderData { VertexSource = "", FragmentSource = "" },
        };
    }

    public const string Refused = "the GPU refused";

    /// <summary>The frames rendered, in order.</summary>
    public List<Frame> Frames { get; } = [];

    public Result RenderFrame(Frame frame)
    {
        Frames.Add(frame);
        return Result.Success();
    }

    private DeletionResult Delete(string kind) =>
        kind == failing ? DeletionResult.Error(new ResultProblem(Refused)) : DeletionResult.Success();

    public Result UploadTexture<TFormat>(TextureId<TFormat, ITextureUsage.Uploaded> textureId, TextureBuffers<TFormat> textureData)
        where TFormat : unmanaged, ITextureFormat => Result.Success();

    public Result CreateTexture<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId, Size textureSize, bool multisampled)
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage.IRenderable => Result.Success();

    public Result ResizeTexture(TextureId textureId, Size textureSize) => Result.Success();

    public DeletionResult DeleteTexture(TextureId textureId) => Delete("texture");

    public ValueTask<Result<TextureBuffers<TFormat>>> ReadTextureAsync<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId)
        where TFormat : unmanaged, ITextureFormat
        where TUsage : ITextureUsage.IRenderable => throw new NotSupportedException();

    public Result CreatePass<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, PassAttachments attachments, TPassInputs inputs)
        where TColor : IColorFormat
        where TPassInputs : IPassInputs => Result.Success();

    Result IPassBackend.SetInputs<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, TPassInputs inputs) => Result.Success();

    public DeletionResult DeletePass(PassId passId) => Delete("pass");

    public Result CreateShader(ShaderId shaderId, ShaderData shaderData) => Result.Success();

    public DeletionResult DeleteShader(ShaderId shaderId) => Delete("shader");

    public Result CreateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
        => Result.Success();

    public Result UpdateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
        => Result.Success();

    public DeletionResult DeleteMesh(MeshId meshId) => Delete("mesh");

    public Result CreateGroup<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, MeshId<TVertex> meshId)
        where TVertex : IVertexData
        where TInstance : IInstanceData => Result.Success();

    public DeletionResult DeleteGroup(GroupId groupId) => Delete("group");

    public Result AddInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId, TInstance instance)
        where TVertex : IVertexData
        where TInstance : IInstanceData => Result.Success();

    public Result UpdateInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId, TInstance instance)
        where TVertex : IVertexData
        where TInstance : IInstanceData => Result.Success();

    public DeletionResult RemoveInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId)
        where TVertex : IVertexData
        where TInstance : IInstanceData => DeletionResult.Success();

    public Result CreateDraw<TInputs>(DrawId<TInputs> drawId, PassId passId, ShaderId shaderId, GroupId groupId, RenderState renderState, TInputs inputs)
        where TInputs : IGroupInputs => Result.Success();

    Result IDrawBackend.SetInputs<TInputs>(DrawId<TInputs> drawId, TInputs inputs) => Result.Success();

    public DeletionResult DeleteDraw(DrawId drawId) => Delete("draw");
}
