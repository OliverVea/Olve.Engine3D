using Olve.Utilities.Ids;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Utilities;

namespace Olve.Engine3D.Rendering.Draws;

/// <summary>The one source of draw ids: a group drawn with a shader in a pass, with the group's inputs.</summary>
public class DrawManager(
    IDrawBackend drawBackend,
    PassManager passManager,
    ShaderManager shaderManager,
    GroupManager groupManager,
    ILogger<DrawManager> logger) : IDisposable
{
    private readonly Dictionary<DrawId, Draw> _draws = [];
    private readonly SegmentOwnership<DrawId> _owners = new();
    private long _nextSequence;

    private sealed record Draw(PassId Pass, ShaderId Shader, GroupId Group, RenderState RenderState, int Key, long Sequence);

    /// <summary>The shader writes the pass's colour formats; the pass, not the shader, decides whether there is depth.</summary>
    public Result<DrawId<TInputs>> Register<TVertex, TInstance, TColor, TPassInputs, TInputs>(
        PassId<TColor, TPassInputs> passId,
        ShaderId<TVertex, TInstance, TColor, TPassInputs, TInputs> shaderId,
        GroupId<TVertex, TInstance> groupId,
        TInputs inputs,
        RenderState renderState,
        int key,
        Id<RenderingSegment> segment)
        where TVertex : IVertexData
        where TInstance : IInstanceData
        where TColor : IColorFormat
        where TPassInputs : IPassInputs
        where TInputs : IGroupInputs
    {
        var drawId = DrawId.New<TInputs>();
        logger.LogDebug("Creating draw {DrawId} of group {GroupId} with shader {ShaderId} in pass {PassId}", drawId, groupId, shaderId, passId);

        if (Result.Chain(
                () => InputsValidator<TInputs>.Default.Validate(inputs),
                () => Claim(passId, shaderId, groupId),
                () => drawBackend.CreateDraw(drawId, passId, shaderId, groupId, renderState, inputs)
                    .IfProblemUndo(() => Release(passId, shaderId, groupId)))
            .TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to create a draw of group '{0}' in pass '{1}'", groupId, passId);
        }

        _draws[drawId] = new Draw(passId, shaderId, groupId, renderState, key, _nextSequence++);
        Version++;
        _owners.Add(segment, drawId);
        logger.LogInformation("Created draw {DrawId} of group {GroupId} with shader {ShaderId} in pass {PassId}", drawId, groupId, shaderId, passId);
        return drawId;
    }

    public Result SetInputs<TInputs>(DrawId<TInputs> drawId, TInputs inputs)
        where TInputs : IGroupInputs
    {
        if (!_draws.ContainsKey(drawId))
        {
            return new ResultProblem("Draw '{0}' is not registered", drawId);
        }

        if (InputsValidator<TInputs>.Default.Validate(inputs).TryPickProblems(out var problems))
        {
            return problems.Prepend("Invalid inputs for draw '{0}'", drawId);
        }

        return drawBackend.SetInputs(drawId, inputs);
    }

    public DeletionResult Delete(DrawId drawId)
    {
        if (!_draws.Remove(drawId, out var draw))
        {
            return DeletionResult.NotFound();
        }

        Version++;
        _owners.Forget(drawId);

        logger.LogDebug("Deleting draw {DrawId}", drawId);

        // The pass, shader and group are released even when the backend fails, or they could never be deleted
        Result deleted = drawBackend.DeleteDraw(drawId).TryPickProblems(out var deleteProblems)
            ? deleteProblems.Prepend("Failed to delete draw '{0}'", drawId)
            : Result.Success();
        if (Result.Concat([deleted, Release(draw.Pass, draw.Shader, draw.Group)]).TryPickProblems(out var problems))
        {
            return DeletionResult.Error(problems);
        }

        logger.LogInformation("Deleted draw {DrawId}", drawId);
        return DeletionResult.Success();
    }

    /// <summary>How many draws are registered.</summary>
    public int Count => _draws.Count;

    /// <summary>Counts the draws registered and deleted, so the order of the draws is worked out again only after one.</summary>
    internal int Version { get; private set; }

    /// <summary>Deletes every draw the segment was the last owner of.</summary>
    public Result DeleteSegment(Id<RenderingSegment> segment) => _owners.Release(segment, Delete);

    internal IReadOnlyList<DrawId> GetOrderedDraws(PassId passId) =>
    [
        .. _draws
            .Where(pair => pair.Value.Pass == passId)
            .OrderBy(pair => pair.Value.Key)
            .ThenBy(pair => pair.Value.RenderState.IsTransparent)
            .ThenBy(pair => pair.Value.Sequence)
            .Select(pair => pair.Key),
    ];

    public void Dispose()
    {
        KeyValuePair<DrawId, Draw>[] draws = [.. _draws];
        _draws.Clear();
        Version++;
        _owners.Clear();

        foreach (var (drawId, draw) in draws)
        {
            var result = drawBackend.DeleteDraw(drawId);
            if (result.TryPickProblems(out var problems))
            {
                logger.LogError("Failed to delete draw {DrawId}: {Problems}", drawId, problems);
            }
            else if (result.WasNotFound)
            {
                logger.LogWarning("Draw {DrawId} was already gone from the draw backend", drawId);
            }

            // The pass, shader and group managers may outlive this scope, so their ids must not stay claimed.
            if (Release(draw.Pass, draw.Shader, draw.Group).TryPickProblems(out problems))
            {
                logger.LogError("Failed to release the pass, shader and group of draw {DrawId}: {Problems}", drawId, problems);
            }
        }
    }

    private Result Claim(PassId passId, ShaderId shaderId, GroupId groupId) =>
        Result.Chain(
            () => passManager.AddUse(passId),
            () => shaderManager.AddUse(shaderId)
                .IfProblemUndo(() => passManager.ReleaseUse(passId)),
            () => groupManager.AddUse(groupId)
                .IfProblemUndo(() => Combine(passManager.ReleaseUse(passId), shaderManager.ReleaseUse(shaderId))));

    private Result Release(PassId passId, ShaderId shaderId, GroupId groupId) =>
        Combine(passManager.ReleaseUse(passId), shaderManager.ReleaseUse(shaderId), groupManager.ReleaseUse(groupId));

    private static Result Combine(params Result[] results)
    {
        List<ResultProblem> problems = [];
        foreach (var result in results)
        {
            if (result.TryPickProblems(out var resultProblems))
            {
                problems.AddRange(resultProblems);
            }
        }

        return problems.Count == 0 ? Result.Success() : new ResultProblemCollection(problems);
    }
}
