using Olve.Utilities.Ids;

namespace Olve.Engine3D.Storage;

public interface IUserStorage
{
    ValueTask<Result<IReadOnlyList<UserStorageEntry>>> ListAsync(Id<UserStorageArea> area, CancellationToken cancellationToken);

    ValueTask<Result<byte[]>> ReadAsync(UserStorageKey key, CancellationToken cancellationToken);

    ValueTask<Result> WriteAsync(UserStorageKey key, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);

    ValueTask<DeletionResult> DeleteAsync(UserStorageKey key, CancellationToken cancellationToken);
}
