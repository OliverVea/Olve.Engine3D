namespace Olve.Engine3D.Assets.Entities;

/// <summary>
/// A shader as the WebGPU backend needs it.
/// </summary>
public class ShaderData
{
    public required string Name { get; init; }

    public required WgslShaderData Wgsl { get; init; }
}

/// <summary>
/// The shader in WGSL, for WebGPU: one module per stage, and where each of its inputs is bound.
/// </summary>
public class WgslShaderData
{
    public required string VertexSource { get; init; }
    public required string FragmentSource { get; init; }

    /// <summary>Where the pass inputs are bound.</summary>
    public WgslInputsBindings Pass { get; init; } = WgslInputsBindings.None;

    /// <summary>Where the group inputs are bound.</summary>
    public WgslInputsBindings Group { get; init; } = WgslInputsBindings.None;
}

/// <summary>
/// Where one inputs struct is bound in WGSL. Every binding is in <c>@group(0)</c>, numbered by Slang per shader.
/// </summary>
/// <param name="Values">The binding of its uniform buffer, or <c>null</c> when the shader doesn't read its values.</param>
/// <param name="Textures">Each texture's bindings, in the order the inputs write them, or <c>null</c> for one the shader doesn't read.</param>
public sealed record WgslInputsBindings(int? Values, IReadOnlyList<WgslTextureBinding?> Textures)
{
    public static WgslInputsBindings None { get; } = new(null, []);
}

/// <param name="Texture">The texture's binding.</param>
/// <param name="Sampler">Its sampler's binding, or <c>null</c> for a texture read without one.</param>
public readonly record struct WgslTextureBinding(int Texture, int? Sampler);
