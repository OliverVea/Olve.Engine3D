using Olve.Utilities.Ids;
using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Rendering.Samplers;

/// <summary>The one source of sampler ids.</summary>
public class SamplerManager(ISamplerBackend samplerBackend, ILogger<SamplerManager> logger) : IDisposable
{
    private readonly HashSet<SamplerId> _samplers = [];
    private readonly SegmentOwnership<SamplerId> _owners = new();

    public Result<SamplerId<TKind>> Register<TKind>(SamplerDescription description, Id<RenderingSegment> segment)
        where TKind : ISamplerKind
    {
        if (new SamplerDescriptionValidator<TKind>().MustSuitKind().Validate(description).TryPickProblems(out var problems))
        {
            return problems;
        }

        var samplerId = SamplerId.New<TKind>();
        logger.LogDebug("Creating {Kind} sampler {SamplerId} ({Description})", typeof(TKind).Name, samplerId, description);

        if (samplerBackend.CreateSampler(samplerId, description).TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to create {0} sampler {1}", typeof(TKind).Name, description);
        }

        _samplers.Add(samplerId);
        _owners.Add(segment, samplerId);
        logger.LogInformation("Created {Kind} sampler {SamplerId}", typeof(TKind).Name, samplerId);
        return samplerId;
    }

    public DeletionResult Delete(SamplerId samplerId)
    {
        if (!_samplers.Remove(samplerId))
        {
            return DeletionResult.NotFound();
        }

        _owners.Forget(samplerId);

        logger.LogDebug("Deleting sampler {SamplerId}", samplerId);

        var result = samplerBackend.DeleteSampler(samplerId);
        if (!result.TryPickProblems(out _))
        {
            logger.LogInformation("Deleted sampler {SamplerId}", samplerId);
        }

        return result;
    }

    /// <summary>How many samplers are registered.</summary>
    public int Count => _samplers.Count;

    /// <summary>Deletes every sampler the segment was the last owner of.</summary>
    public Result DeleteSegment(Id<RenderingSegment> segment) => _owners.Release(segment, Delete);

    public void Dispose()
    {
        SamplerId[] samplerIds = [.. _samplers];
        _samplers.Clear();
        _owners.Clear();

        foreach (var samplerId in samplerIds)
        {
            var result = samplerBackend.DeleteSampler(samplerId);
            if (result.TryPickProblems(out var problems))
            {
                logger.LogError("Failed to delete sampler {SamplerId}: {Problems}", samplerId, problems);
            }
            else if (result.WasNotFound)
            {
                logger.LogWarning("Sampler {SamplerId} was already gone from the sampler backend", samplerId);
            }
        }
    }
}
