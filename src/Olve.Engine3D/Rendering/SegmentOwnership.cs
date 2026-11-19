using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering;

/// <summary>Which segments own which of a manager's ids; an id goes when the last segment owning it is released.</summary>
internal sealed class SegmentOwnership<TId> where TId : notnull
{
    private readonly Dictionary<Id<RenderingSegment>, HashSet<TId>> _owned = [];
    private readonly Dictionary<TId, HashSet<Id<RenderingSegment>>> _owners = [];

    public void Add(Id<RenderingSegment> segment, TId id)
    {
        if (!_owned.TryGetValue(segment, out var owned))
        {
            _owned[segment] = owned = [];
        }

        if (!_owners.TryGetValue(id, out var owners))
        {
            _owners[id] = owners = [];
        }

        owned.Add(id);
        owners.Add(segment);
    }

    /// <summary>Forgets an id that was deleted: no segment owns it any more.</summary>
    public void Forget(TId id)
    {
        if (!_owners.Remove(id, out var owners))
        {
            return;
        }

        foreach (var segment in owners)
        {
            if (_owned.TryGetValue(segment, out var owned) && owned.Remove(id) && owned.Count == 0)
            {
                _owned.Remove(segment);
            }
        }
    }

    /// <summary>Releases the segment: deletes every id it was the last owner of, carrying on past a failed delete.</summary>
    public Result Release(Id<RenderingSegment> segment, Func<TId, DeletionResult> delete)
    {
        if (!_owned.Remove(segment, out var owned))
        {
            return Result.Success();
        }

        List<Result> failures = [];
        foreach (var id in owned)
        {
            if (_owners.TryGetValue(id, out var owners) && owners.Remove(segment) && owners.Count > 0)
            {
                continue;
            }

            _owners.Remove(id);
            var result = delete(id);
            if (result.WasNotFound)
            {
                failures.Add(new ResultProblem("'{0}' was owned but isn't registered", id));
            }
            else if (result.TryPickProblems(out var problems))
            {
                failures.Add(problems);
            }
        }

        return Result.Concat([.. failures]);
    }

    public void Clear()
    {
        _owned.Clear();
        _owners.Clear();
    }
}
