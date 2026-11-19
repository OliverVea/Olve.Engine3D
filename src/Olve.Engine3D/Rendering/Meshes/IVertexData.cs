namespace Olve.Engine3D.Rendering.Meshes;

/// <summary>One vertex as a shader reads it: its floats and their layout in the vertex buffer.</summary>
public interface IVertexData
{
    void WriteTo(Span<float> buffer);
    static abstract int FloatCount { get; }
    static abstract VertexBufferLayout Layout { get; }

    /// <summary>The vertex of a shader without vertex inputs: its meshes hold only a vertex count.</summary>
    public readonly record struct None : IVertexData
    {
        public static int FloatCount => 0;
        public static VertexBufferLayout Layout { get; } = new(0, false, []);

        public void WriteTo(Span<float> buffer)
        {
        }
    }
}
