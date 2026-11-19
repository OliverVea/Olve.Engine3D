using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering.Samplers;

/// <summary>Sampler id without its kind. Equal to any sampler id, typed or not, with the same <see cref="Value"/>.</summary>
public class SamplerId : IEquatable<SamplerId>
{
    internal SamplerId(Id value)
    {
        Value = value;
    }

    public Id Value { get; }

    public static SamplerId<TKind> New<TKind>()
        where TKind : ISamplerKind
        => new(Id.New());

    public bool Equals(SamplerId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is SamplerId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(SamplerId? left, SamplerId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(SamplerId? left, SamplerId? right) => !(left == right);
}

/// <summary>Id of a <typeparamref name="TKind"/> sampler.</summary>
public sealed class SamplerId<TKind> : SamplerId
    where TKind : ISamplerKind
{
    internal SamplerId(Id value) : base(value)
    {
    }
}
