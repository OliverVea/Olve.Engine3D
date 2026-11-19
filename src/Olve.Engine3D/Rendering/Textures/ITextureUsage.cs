namespace Olve.Engine3D.Rendering.Textures;

/// <summary>What a texture can be used for: the second type argument of <see cref="TextureId{TFormat, TUsage}"/>.</summary>
public interface ITextureUsage
{
    /// <summary>A texture usage that shaders can read: sampled, or loaded without a sampler.</summary>
    public interface ISampleable : ITextureUsage;

    /// <summary>A texture usage that a render pass can draw into.</summary>
    public interface IRenderable : ITextureUsage;

    /// <summary>Read by shaders only, e.g. a loaded asset. Usually from the CPU.</summary>
    public sealed class Uploaded : ISampleable;

    /// <summary>Drawn into only, never read by a shader. Always from the GPU.</summary>
    public sealed class RenderTarget : IRenderable;

    /// <summary>Drawn into, then read by a later pass, e.g. the shadow map. Always from the GPU.</summary>
    public sealed class SampledRenderTarget : ISampleable, IRenderable;
}
