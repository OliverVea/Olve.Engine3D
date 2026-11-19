using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.GUI.Elements;

public interface IRenderableAsRectangle
{
    record Data
    {
        public RGBA Color { get; init; } = RGBA.White;
        public TextureId<ITextureFormat.Rgba8Unorm, ITextureUsage.Uploaded>? Texture { get; init; }
        public Border? Border { get; init; }
        public Vector2 UvMin { get; init; } = Vector2.Zero;
        public Vector2 UvMax { get; init; } = Vector2.One;
    }

    Data TexturedRectangleData { get; }
}
