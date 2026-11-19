using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering.Instancing;

/// <summary>Instance id without its instance type. Equal to any instance id, typed or not, with the same <see cref="Value"/>.</summary>
public class InstanceId : IEquatable<InstanceId>
{
    internal InstanceId(Id value)
    {
        Value = value;
    }

    public Id Value { get; }

    public static InstanceId<TInstance> New<TInstance>()
        where TInstance : IInstanceData
        => new(Id.New());

    public bool Equals(InstanceId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is InstanceId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(InstanceId? left, InstanceId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(InstanceId? left, InstanceId? right) => !(left == right);
}

/// <summary>Id of a <typeparamref name="TInstance"/> instance in a group.</summary>
public sealed class InstanceId<TInstance> : InstanceId
    where TInstance : IInstanceData
{
    internal InstanceId(Id value) : base(value)
    {
    }
}
