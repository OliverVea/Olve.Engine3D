using Olve.Utilities.Ids;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Utilities;
using Olve.Validation;

namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>The one source of pass ids: what a pass draws into, the inputs its draws share, and when it runs.</summary>
public class PassManager(IPassBackend passBackend, TextureManager textureManager, ILogger<PassManager> logger) : IDisposable
{
    private readonly Dictionary<PassId, Pass> _passes = [];
    private readonly UseCounts<PassId> _uses = new();
    private readonly SegmentOwnership<PassId> _owners = new();
    private readonly IValidator<IReadOnlyList<TextureId>> _attachmentsValidator =
        new AttachmentsValidator(textureManager).MustBeCreatedTextures().MustBeSameSize().MustAgreeOnSamples();
    private long _nextSequence;

    private sealed record Pass(TextureId[] Textures, int Priority, long Sequence);

    public Result<PassId<IColorFormat<T0>, TPassInputs>> Register<T0, TPassInputs>(
        ColorAttachment<T0> color,
        TPassInputs inputs,
        int priority,
        Id<RenderingSegment> segment)
        where T0 : ITextureFormat.IColor
        where TPassInputs : IPassInputs
        => RegisterCore<IColorFormat<T0>, TPassInputs>(new PassAttachments(color, null), inputs, priority, segment);

    public Result<PassId<IColorFormat<T0>, TPassInputs>> Register<T0, TPassInputs>(
        ColorAttachment<T0> color,
        DepthAttachment depth,
        TPassInputs inputs,
        int priority,
        Id<RenderingSegment> segment)
        where T0 : ITextureFormat.IColor
        where TPassInputs : IPassInputs
        => RegisterCore<IColorFormat<T0>, TPassInputs>(new PassAttachments(color, depth), inputs, priority, segment);

    public Result<PassId<IColorFormat.None, TPassInputs>> Register<TPassInputs>(
        DepthAttachment depth,
        TPassInputs inputs,
        int priority,
        Id<RenderingSegment> segment)
        where TPassInputs : IPassInputs
        => RegisterCore<IColorFormat.None, TPassInputs>(new PassAttachments(null, depth), inputs, priority, segment);

    public Result SetInputs<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, TPassInputs inputs)
        where TColor : IColorFormat
        where TPassInputs : IPassInputs
    {
        if (!IsRegistered(passId))
        {
            return new ResultProblem("Pass '{0}' is not registered", passId);
        }

        if (InputsValidator<TPassInputs>.Default.Validate(inputs).TryPickProblems(out var problems))
        {
            return problems.Prepend("Invalid inputs for pass '{0}'", passId);
        }

        return passBackend.SetInputs(passId, inputs);
    }

    public DeletionResult Delete(PassId passId)
    {
        if (!_passes.TryGetValue(passId, out var pass))
        {
            return DeletionResult.NotFound();
        }

        if (_uses.Get(passId) is var uses and > 0)
        {
            return new ResultProblem("Pass '{0}' is still used by {1} consumer(s)", passId, uses);
        }

        logger.LogDebug("Deleting pass {PassId}", passId);
        _passes.Remove(passId);
        Version++;
        _owners.Forget(passId);

        // The textures are released even when the backend fails, or they could never be deleted
        Result deleted = passBackend.DeletePass(passId).TryPickProblems(out var deleteProblems)
            ? deleteProblems.Prepend("Failed to delete pass '{0}'", passId)
            : Result.Success();
        if (Result.Concat([deleted, textureManager.ReleaseUses(pass.Textures)]).TryPickProblems(out var problems))
        {
            return DeletionResult.Error(problems);
        }

        logger.LogInformation("Deleted pass {PassId}", passId);
        return DeletionResult.Success();
    }

    /// <summary>How many passs are registered.</summary>
    public int Count => _passes.Count;

    /// <summary>Counts the passes registered and deleted, so the order of the passes is worked out again only after one.</summary>
    internal int Version { get; private set; }

    /// <summary>Deletes every pass the segment was the last owner of.</summary>
    public Result DeleteSegment(Id<RenderingSegment> segment) => _owners.Release(segment, Delete);

    internal Result AddUse(PassId passId)
    {
        if (!_passes.ContainsKey(passId))
        {
            return new ResultProblem("Pass '{0}' is not registered", passId);
        }

        _uses.Add(passId);
        return Result.Success();
    }

    internal Result ReleaseUse(PassId passId) =>
        _uses.TryRelease(passId)
            ? Result.Success()
            : new ResultProblem("Pass '{0}' has no use to release", passId);

    internal IReadOnlyList<PassId> GetOrderedPasses() =>
    [
        .. _passes
            .OrderBy(pair => pair.Value.Priority)
            .ThenBy(pair => pair.Value.Sequence)
            .Select(pair => pair.Key),
    ];

    public void Dispose()
    {
        KeyValuePair<PassId, Pass>[] passes = [.. _passes];
        _passes.Clear();
        Version++;
        _owners.Clear();
        _uses.Clear();

        foreach (var (passId, pass) in passes)
        {
            var result = passBackend.DeletePass(passId);
            if (result.TryPickProblems(out var problems))
            {
                logger.LogError("Failed to delete pass {PassId}: {Problems}", passId, problems);
            }
            else if (result.WasNotFound)
            {
                logger.LogWarning("Pass {PassId} was already gone from the pass backend", passId);
            }

            // The texture manager may outlive this scope, so its textures must not stay claimed.
            if (textureManager.ReleaseUses(pass.Textures).TryPickProblems(out problems))
            {
                logger.LogError("Failed to release the attachments of pass {PassId}: {Problems}", passId, problems);
            }
        }
    }

    private Result<PassId<TColor, TPassInputs>> RegisterCore<TColor, TPassInputs>(
        PassAttachments attachments,
        TPassInputs inputs,
        int priority,
        Id<RenderingSegment> segment)
        where TColor : IColorFormat
        where TPassInputs : IPassInputs
    {
        var passId = PassId.New<TColor, TPassInputs>();
        TextureId[] textures = [.. new[] { attachments.Color?.Texture, attachments.Depth?.Texture }.OfType<TextureId>()];
        logger.LogDebug("Creating {Format} pass {PassId} at priority {Priority}", typeof(TColor).Name, passId, priority);

        if (Result.Chain(
                () => InputsValidator<TPassInputs>.Default.Validate(inputs),
                () => _attachmentsValidator.Validate(textures),
                () => textureManager.AddUses(textures),
                () => passBackend.CreatePass(passId, attachments, inputs).IfProblemUndo(() => textureManager.ReleaseUses(textures)))
            .TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to create a pass drawing into {0}", typeof(TColor).Name);
        }

        _passes[passId] = new Pass(textures, priority, _nextSequence++);
        Version++;
        _owners.Add(segment, passId);
        logger.LogInformation("Created {Format} pass {PassId} at priority {Priority}", typeof(TColor).Name, passId, priority);
        return passId;
    }

    private bool IsRegistered(PassId passId) => _passes.ContainsKey(passId);
}
