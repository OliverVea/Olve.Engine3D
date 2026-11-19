using Olve.Engine3D.Rendering.Shaders;

namespace Olve.Engine3D.Rendering.Instancing;

/// <summary>The inputs of one draw, declared by its shader's group inputs block and group textures.</summary>
public interface IGroupInputs : IInputs
{
    /// <summary>
    /// Group inputs of a shader that declares no group-inputs block and no textures. The draw has no uniform buffer.
    /// </summary>
    public readonly record struct None : IGroupInputs
    {
        public static int FloatCount => 0;
        public static int TextureCount => 0;

        public void WriteTo(Span<float> buffer)
        {
        }

        public void WriteTextures(Span<TextureBinding> textures)
        {
        }
    }
}
