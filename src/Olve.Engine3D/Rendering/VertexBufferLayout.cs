namespace Olve.Engine3D.Rendering;

/// <summary>
/// How a vertex buffer's elements map to shader inputs. <paramref name="Instanced"/> buffers advance once per instance
/// instead of once per vertex. Inputs spanning several locations (e.g. a mat4) have one attribute per location.
/// </summary>
public sealed record VertexBufferLayout(int StrideBytes, bool Instanced, IReadOnlyList<VertexAttributeLayout> Attributes);

/// <summary>
/// One shader input in a vertex buffer: <paramref name="ComponentCount"/> 32-bit floats
/// starting <paramref name="ByteOffset"/> bytes into each element.
/// </summary>
public readonly record struct VertexAttributeLayout(uint Location, int ComponentCount, int ByteOffset);
