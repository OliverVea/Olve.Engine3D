using Olve.Engine3D.Rendering.Textures;
using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>The WebGPU format of each texture format, and its bytes per pixel.</summary>
internal static class WebGPUTextureFormats
{
    public readonly record struct Formats(TextureFormat Format, int BytesPerPixel)
    {
        public bool IsDepth => Format == TextureFormat.Depth32Float;
    }

    public static Result<Formats> Get(Type format)
    {
        if (format == typeof(ITextureFormat.Rgba8Unorm))
        {
            return new Formats(TextureFormat.RGBA8Unorm, 4);
        }

        if (format == typeof(ITextureFormat.R32Float))
        {
            return new Formats(TextureFormat.R32Float, 4);
        }

        if (format == typeof(ITextureFormat.Depth))
        {
            return new Formats(TextureFormat.Depth32Float, 4);
        }

        return new ResultProblem("Texture format '{0}' has no WebGPU mapping", format.Name);
    }
}
