using Olve.Engine3D.Rendering.Instancing;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering.Draws;

/// <summary>Draw id without its group inputs type. Equal to any draw id, typed or not, with the same <see cref="Value"/>.</summary>
public class DrawId : IEquatable<DrawId>
{
    internal DrawId(Id value)
    {
        Value = value;
    }

    public Id Value { get; }

    public static DrawId<TInputs> New<TInputs>()
        where TInputs : IGroupInputs
        => new(Id.New());

    public bool Equals(DrawId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is DrawId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(DrawId? left, DrawId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(DrawId? left, DrawId? right) => !(left == right);
}

/// <summary>Id of a draw whose shader reads <typeparamref name="TInputs"/>.</summary>
public sealed class DrawId<TInputs> : DrawId
    where TInputs : IGroupInputs
{
    internal DrawId(Id value) : base(value)
    {
    }
}
