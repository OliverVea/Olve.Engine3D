using Olve.Engine3D.Rendering.Shaders;

namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>The inputs shared by every draw in a pass, declared once in <c>passes.slang</c>.</summary>
public interface IPassInputs : IInputs
{
    /// <summary>
    /// Pass inputs of a pass whose shaders declare no pass-inputs block. The pass has no uniform buffer and no textures.
    /// </summary>
    public readonly record struct None : IPassInputs
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
