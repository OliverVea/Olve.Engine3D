using Olve.Engine3D.Rendering.Meshes;

namespace Olve.Engine3D.Rendering.Instancing;

/// <summary>One instance as a shader reads it, laid out like a vertex in a buffer that advances once per instance.</summary>
public interface IInstanceData : IVertexData;
