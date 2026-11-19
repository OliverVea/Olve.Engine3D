using Olve.Utilities.Ids;

namespace Olve.Engine3D.Storage;

public sealed class InMemoryUserStorage(TimeProvider timeProvider) : IUserStorage
{
    private readonly Lock _lock = new();
    private readonly Dictionary<UserStorageKey, (byte[] Bytes, DateTimeOffset Modified)> _entries = [];

    public ValueTask<Result<IReadOnlyList<UserStorageEntry>>> ListAsync(Id<UserStorageArea> area, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyList<UserStorageEntry> entries = _entries
                .Where(entry => entry.Key.Area == area)
                .Select(entry => new UserStorageEntry(entry.Key, entry.Value.Modified))
                .OrderBy(entry => entry.Key.Entry)
                .ToList();
            return ValueTask.FromResult(Result<IReadOnlyList<UserStorageEntry>>.Success(entries));
        }
    }

    public ValueTask<Result<byte[]>> ReadAsync(UserStorageKey key, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return ValueTask.FromResult<Result<byte[]>>(_entries.TryGetValue(key, out var entry)
                ? entry.Bytes.ToArray()
                : new NotFoundProblem("Nothing is stored at '{0}'", key));
        }
    }

    public ValueTask<Result> WriteAsync(UserStorageKey key, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _entries[key] = (bytes.ToArray(), timeProvider.GetUtcNow());
            return ValueTask.FromResult(Result.Success());
        }
    }

    public ValueTask<DeletionResult> DeleteAsync(UserStorageKey key, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return ValueTask.FromResult(_entries.Remove(key) ? DeletionResult.Success() : DeletionResult.NotFound());
        }
    }
}
