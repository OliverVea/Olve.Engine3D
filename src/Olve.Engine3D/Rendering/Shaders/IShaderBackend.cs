using Olve.Engine3D.Assets.Entities;

namespace Olve.Engine3D.Rendering.Shaders;

/// <summary>Compiles and deletes shaders on the GPU.</summary>
public interface IShaderBackend
{
    Result CreateShader(ShaderId shaderId, ShaderData shaderData);

    DeletionResult DeleteShader(ShaderId shaderId);
}
