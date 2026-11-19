namespace Olve.Engine3D.Rendering.Shaders;

/// <summary>
/// The values and textures of one inputs block: values laid out as a std140 uniform block, textures in the order the
/// shader declares them.
/// </summary>
public interface IInputs
{
    void WriteTo(Span<float> buffer);
    static abstract int FloatCount { get; }

    void WriteTextures(Span<TextureBinding> textures);
    static abstract int TextureCount { get; }
}
