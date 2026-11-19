using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>The colour outputs of a shader and the colour attachments of a pass: the type argument they are matched by.</summary>
public interface IColorFormat
{
    /// <summary>No colour: the shader or pass only writes depth.</summary>
    public interface None : IColorFormat;
}

/// <summary>One <typeparamref name="T0"/> colour output or attachment.</summary>
public interface IColorFormat<T0> : IColorFormat where T0 : ITextureFormat.IColor;
