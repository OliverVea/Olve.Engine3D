namespace Olve.Engine3D.Rendering;

/// <summary>How many references each id has; a manager refuses to delete an id that still has some.</summary>
internal sealed class UseCounts<TId> where TId : notnull
{
    private readonly Dictionary<TId, int> _counts = new();

    public void Add(TId id) => _counts[id] = _counts.GetValueOrDefault(id) + 1;

    public bool TryRelease(TId id)
    {
        if (!_counts.TryGetValue(id, out var count))
        {
            return false;
        }

        if (count == 1)
        {
            _counts.Remove(id);
        }
        else
        {
            _counts[id] = count - 1;
        }

        return true;
    }

    public int Get(TId id) => _counts.GetValueOrDefault(id);

    public void Clear() => _counts.Clear();
}
