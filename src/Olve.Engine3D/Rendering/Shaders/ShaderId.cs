using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering.Shaders;

/// <summary>Shader id without its vertex, instance, colour format, pass inputs and group inputs types. Equal to any shader id, typed or not, with the same <see cref="Value"/>.</summary>
public class ShaderId : IEquatable<ShaderId>
{
    internal ShaderId(Id value)
    {
        Value = value;
    }

    public Id Value { get; }

    public static ShaderId<TVertex, TInstance, TColor, TPassInputs, TInputs> New<TVertex, TInstance, TColor, TPassInputs, TInputs>()
        where TVertex : IVertexData
        where TInstance : IInstanceData
        where TColor : IColorFormat
        where TPassInputs : IPassInputs
        where TInputs : IGroupInputs
        => new(Id.New());

    public bool Equals(ShaderId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is ShaderId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(ShaderId? left, ShaderId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(ShaderId? left, ShaderId? right) => !(left == right);
}

/// <summary>Id of a shader drawing <typeparamref name="TVertex"/> meshes once per <typeparamref name="TInstance"/> into <typeparamref name="TColor"/>.</summary>
public sealed class ShaderId<TVertex, TInstance, TColor, TPassInputs, TInputs> : ShaderId
    where TVertex : IVertexData
    where TInstance : IInstanceData
    where TColor : IColorFormat
    where TPassInputs : IPassInputs
    where TInputs : IGroupInputs
{
    internal ShaderId(Id value) : base(value)
    {
    }
}

