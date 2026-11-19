using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;

namespace Olve.Engine3D.Rendering.Shaders;

/// <summary>A compiled shader's data, without its types.</summary>
public interface IShaderDefinition
{
    ShaderData ShaderData { get; }
}

/// <summary>Definition of a shader drawing <typeparamref name="TVertex"/> meshes once per <typeparamref name="TInstance"/> into <typeparamref name="TColor"/>.</summary>
public interface IShaderDefinition<TVertex, TInstance, TColor, TPassInputs, TInputs> : IShaderDefinition
    where TVertex : IVertexData
    where TInstance : IInstanceData
    where TColor : IColorFormat
    where TPassInputs : IPassInputs
    where TInputs : IGroupInputs;

