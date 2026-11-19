using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>What a pass does with an attachment's previous contents.</summary>
public abstract record Load<TFormat> where TFormat : ITextureFormat
{
    private Load()
    {
    }

    public sealed record Clear(TFormat Value) : Load<TFormat>;

    public sealed record Keep : Load<TFormat>;
}
