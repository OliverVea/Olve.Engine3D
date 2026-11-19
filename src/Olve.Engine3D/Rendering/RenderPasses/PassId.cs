using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>Pass id without its colour format and pass inputs types. Equal to any pass id, typed or not, with the same <see cref="Value"/>.</summary>
public class PassId : IEquatable<PassId>
{
    internal PassId(Id value)
    {
        Value = value;
    }

    public Id Value { get; }

    public static PassId<TColor, TPassInputs> New<TColor, TPassInputs>()
        where TColor : IColorFormat
        where TPassInputs : IPassInputs
        => new(Id.New());

    public bool Equals(PassId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is PassId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(PassId? left, PassId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(PassId? left, PassId? right) => !(left == right);
}

/// <summary>
/// Id of a pass drawing colour into <typeparamref name="TColor"/>; only shaders writing <typeparamref name="TColor"/> and
/// declaring <typeparamref name="TPassInputs"/> can be drawn in it. Whether it has depth is up to its attachments.
/// </summary>
public sealed class PassId<TColor, TPassInputs> : PassId
    where TColor : IColorFormat
    where TPassInputs : IPassInputs
{
    internal PassId(Id value) : base(value)
    {
    }
}
