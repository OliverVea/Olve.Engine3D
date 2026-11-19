using Olve.Engine3D.Rendering.Meshes;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering.Instancing;

/// <summary>Group id without its vertex and instance types. Equal to any group id, typed or not, with the same <see cref="Value"/>.</summary>
public class GroupId : IEquatable<GroupId>
{
    internal GroupId(Id value)
    {
        Value = value;
    }

    public Id Value { get; }

    public static GroupId<TVertex, TInstance> New<TVertex, TInstance>()
        where TVertex : IVertexData
        where TInstance : IInstanceData
        => new(Id.New());

    public bool Equals(GroupId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is GroupId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(GroupId? left, GroupId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(GroupId? left, GroupId? right) => !(left == right);
}

/// <summary>Id of a group drawing a <typeparamref name="TVertex"/> mesh once per <typeparamref name="TInstance"/> instance.</summary>
public sealed class GroupId<TVertex, TInstance> : GroupId
    where TVertex : IVertexData
    where TInstance : IInstanceData
{
    internal GroupId(Id value) : base(value)
    {
    }
}
