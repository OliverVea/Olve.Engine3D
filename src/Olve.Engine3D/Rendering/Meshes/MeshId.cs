using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering.Meshes;

/// <summary>Mesh id without its vertex type. Equal to any mesh id, typed or not, with the same <see cref="Value"/>.</summary>
public class MeshId : IEquatable<MeshId>
{
    internal MeshId(Id value)
    {
        Value = value;
    }

    public Id Value { get; }

    public static MeshId<TVertex> New<TVertex>()
        where TVertex : IVertexData
        => new(Id.New());

    public bool Equals(MeshId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is MeshId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(MeshId? left, MeshId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(MeshId? left, MeshId? right) => !(left == right);
}

/// <summary>Id of a mesh of <typeparamref name="TVertex"/> vertices.</summary>
public sealed class MeshId<TVertex> : MeshId
    where TVertex : IVertexData
{
    internal MeshId(Id value) : base(value)
    {
    }
}
