using System.Runtime.InteropServices;
using Olve.Engine3D.Rendering.Samplers;

namespace Olve.Engine3D.Rendering.Textures;

/// <summary>
/// A texture's format on the GPU, e.g. <see cref="Rgba8Unorm"/>. Backend-neutral: each backend maps every format to
/// its own values.
/// </summary>
public interface ITextureFormat
{
    /// <summary>A format a pass can draw colour into.</summary>
    public interface IColor : ITextureFormat;

    /// <summary>A format a pass can test and write depth in.</summary>
    public interface IDepth : ITextureFormat;

    /// <summary>
    /// Four 8-bit channels, read as floats in [0, 1].
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Rgba8Unorm(byte R, byte G, byte B, byte A) : IColor, ISampledWith<ISamplerKind.Filtering>, ISampledWith<ISamplerKind.NonFiltering>
    {
        public static readonly Rgba8Unorm White = new(255, 255, 255, 255);
    }
    
    /// <summary>
    /// A 32-bit float depth texture. Rendered into, never uploaded.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Depth(float Value) : IDepth, ISampledWith<ISamplerKind.Comparison>
    {
        public static implicit operator Depth(float value) => new(value);
    }
    
    /// <summary>
    /// One 32-bit float channel.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct R32Float(float Value) : IColor, ISampledWith<ISamplerKind.NonFiltering>
    {
        public static implicit operator R32Float(float value) => new(value);
    }
}
