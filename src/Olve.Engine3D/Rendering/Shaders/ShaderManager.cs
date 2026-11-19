using Olve.Utilities.Ids;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;

namespace Olve.Engine3D.Rendering.Shaders;

/// <summary>The one source of shader ids; each definition type is compiled once.</summary>
public class ShaderManager(IShaderBackend shaderBackend, ILogger<ShaderManager> logger) : IDisposable
{
    private readonly Dictionary<Type, ShaderId> _idsByDefinition = [];
    private readonly Dictionary<ShaderId, IShaderDefinition> _shaders = [];
    private readonly UseCounts<ShaderId> _uses = new();
    private readonly SegmentOwnership<ShaderId> _owners = new();

    public Result<ShaderId<TVertex, TInstance, TColor, TPassInputs, TInputs>> Register<TVertex, TInstance, TColor, TPassInputs, TInputs>(
        IShaderDefinition<TVertex, TInstance, TColor, TPassInputs, TInputs> definition,
        Id<RenderingSegment> segment)
        where TVertex : IVertexData
        where TInstance : IInstanceData
        where TColor : IColorFormat
        where TPassInputs : IPassInputs
        where TInputs : IGroupInputs
    {
        if (_idsByDefinition.TryGetValue(definition.GetType(), out var cachedId))
        {
            _owners.Add(segment, cachedId);
            return (ShaderId<TVertex, TInstance, TColor, TPassInputs, TInputs>)cachedId;
        }

        var shaderId = ShaderId.New<TVertex, TInstance, TColor, TPassInputs, TInputs>();
        if (RegisterCore(shaderId, definition).TryPickProblems(out var problems))
        {
            return problems;
        }

        _owners.Add(segment, shaderId);
        return shaderId;
    }

    public DeletionResult Delete(ShaderId shaderId)
    {
        if (!_shaders.TryGetValue(shaderId, out var definition))
        {
            return DeletionResult.NotFound();
        }

        if (_uses.Get(shaderId) is var uses and > 0)
        {
            return new ResultProblem("Shader '{0}' is still used by {1} consumer(s)", definition.ShaderData.Name, uses);
        }

        logger.LogDebug("Deleting shader '{Name}' ({ShaderId})", definition.ShaderData.Name, shaderId);
        _shaders.Remove(shaderId);
        _idsByDefinition.Remove(definition.GetType());
        _owners.Forget(shaderId);

        var result = shaderBackend.DeleteShader(shaderId);
        if (!result.TryPickProblems(out _))
        {
            logger.LogInformation("Deleted shader '{Name}' ({ShaderId})", definition.ShaderData.Name, shaderId);
        }

        return result;
    }

    /// <summary>How many shaders are registered.</summary>
    public int Count => _shaders.Count;

    /// <summary>Deletes every shader the segment was the last owner of.</summary>
    public Result DeleteSegment(Id<RenderingSegment> segment) => _owners.Release(segment, Delete);

    internal Result AddUse(ShaderId shaderId)
    {
        if (!_shaders.ContainsKey(shaderId))
        {
            return new ResultProblem("Shader '{0}' is not registered", shaderId);
        }

        _uses.Add(shaderId);
        return Result.Success();
    }

    internal Result ReleaseUse(ShaderId shaderId) =>
        _uses.TryRelease(shaderId)
            ? Result.Success()
            : new ResultProblem("Shader '{0}' has no use to release", shaderId);

    public void Dispose()
    {
        KeyValuePair<ShaderId, IShaderDefinition>[] shaders = [.. _shaders];
        _shaders.Clear();
        _idsByDefinition.Clear();
        _owners.Clear();
        _uses.Clear();

        foreach (var (shaderId, definition) in shaders)
        {
            var result = shaderBackend.DeleteShader(shaderId);
            if (result.TryPickProblems(out var problems))
            {
                logger.LogError("Failed to delete shader '{Name}' ({ShaderId}): {Problems}", definition.ShaderData.Name, shaderId, problems);
            }
            else if (result.WasNotFound)
            {
                logger.LogWarning("Shader '{Name}' ({ShaderId}) was already gone from the shader backend", definition.ShaderData.Name, shaderId);
            }
        }
    }

    private Result RegisterCore(ShaderId shaderId, IShaderDefinition definition)
    {
        var name = definition.ShaderData.Name;
        logger.LogDebug("Compiling shader '{Name}' ({ShaderId})", name, shaderId);

        if (shaderBackend.CreateShader(shaderId, definition.ShaderData).TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to compile shader '{0}'", name);
        }

        _shaders[shaderId] = definition;
        _idsByDefinition[definition.GetType()] = shaderId;
        logger.LogInformation("Compiled shader '{Name}' ({ShaderId})", name, shaderId);
        return Result.Success();
    }
}
