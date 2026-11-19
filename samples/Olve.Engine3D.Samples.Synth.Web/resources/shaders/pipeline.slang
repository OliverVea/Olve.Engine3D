// The attributes and vertex input types the asset pipeline reads from Slang reflection to generate each shader's C# types.
module pipeline;

/// A vertex input that is per instance rather than per vertex.
[__AttributeUsage(_AttributeTargets.Var)]
public struct InstancedAttribute {};

/// A vertex input that implements an interface property, e.g. [Implements("IWithPosition3D.Position")].
[__AttributeUsage(_AttributeTargets.Var)]
public struct ImplementsAttribute { public string member; };

/// The format of a texture or a fragment output, e.g. [PixelType("Rgba8Unorm")].
[__AttributeUsage(_AttributeTargets.Var)]
public struct PixelTypeAttribute { public string pixelType; };

/// The kind of sampler a Sampler2D or Sampler2DShadow is read through: Filtering, NonFiltering or Comparison, e.g. [Sampler("Filtering")].
[__AttributeUsage(_AttributeTargets.Var)]
public struct SamplerAttribute { public string kind; };

/// A constant buffer or texture shared by every draw in a pass, e.g. [PassInputs("MainPass")].
[__AttributeUsage(_AttributeTargets.Var)]
public struct PassInputsAttribute { public string name; };

/// A constant buffer or texture of one rendering group: [GroupInputs] for the shader's own Inputs type, or
/// [GroupInputs("Name")] to share a constant buffer as Shaders.Shared.Name.
[__AttributeUsage(_AttributeTargets.Var)]
public struct GroupInputsAttribute { public string name = ""; };

/// A vertex input parameter whose type several shaders share, e.g. [SharedVertex("MeshVertex")].
[__AttributeUsage(_AttributeTargets.Param)]
public struct SharedVertexAttribute { public string name; };

/// A 4x4 matrix vertex input, passed as four rows because WGSL has no matrix vertex inputs.
/// The C# side sees one Matrix4x4 spanning four locations.
public struct Matrix4Rows
{
    public float4 row0;
    public float4 row1;
    public float4 row2;
    public float4 row3;

    public float4x4 toMatrix() { return float4x4(row0, row1, row2, row3); }
};
