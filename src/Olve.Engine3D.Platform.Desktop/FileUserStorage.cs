using Olve.Engine3D.Storage;
using Olve.Paths;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Platform.Desktop;

public sealed class FileUserStorage(IPath root, TimeProvider timeProvider) : IUserStorage
{
    private const string TemporarySuffix = ".tmp";

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<Result<IReadOnlyList<UserStorageEntry>>> ListAsync(Id<UserStorageArea> area, CancellationToken cancellationToken)
    {
        var folder = root / area.ToString();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!folder.Exists())
            {
                return Result<IReadOnlyList<UserStorageEntry>>.Success([]);
            }

            return new DirectoryInfo(folder.Path)
                .EnumerateFiles()
                .SelectMany(file => Id.TryParse<UserStorageEntry>(file.Name, out var entry) && entry.ToString() == file.Name
                    ? new[] { new UserStorageEntry(new UserStorageKey(area, entry), new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)) }
                    : [])
                .OrderBy(entry => entry.Key.Entry)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem(exception, "Failed to list user storage area '{0}' in '{1}'", area, folder.Path);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<Result<byte[]>> ReadAsync(UserStorageKey key, CancellationToken cancellationToken)
    {
        var file = Locate(key);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await File.ReadAllBytesAsync(file.Path, cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new NotFoundProblem("Nothing is stored at '{0}'", key);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem(exception, "Failed to read '{0}' from '{1}'", key, file.Path);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<Result> WriteAsync(UserStorageKey key, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        var file = Locate(key);
        var temporary = file.Parent / $"{key.Entry}.{Guid.NewGuid():N}{TemporarySuffix}";
        await _gate.WaitAsync(cancellationToken);
        try
        {
            file.Parent.EnsurePathExists();
            await File.WriteAllBytesAsync(temporary.Path, bytes, cancellationToken);
            File.SetLastWriteTimeUtc(temporary.Path, timeProvider.GetUtcNow().UtcDateTime);
            File.Move(temporary.Path, file.Path, overwrite: true);
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);
            return new ResultProblem(exception, "Failed to write '{0}' to '{1}'", key, file.Path);
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporary);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<DeletionResult> DeleteAsync(UserStorageKey key, CancellationToken cancellationToken)
    {
        var file = Locate(key);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!file.Exists())
            {
                return DeletionResult.NotFound();
            }

            File.Delete(file.Path);
            return DeletionResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DeletionResult.Error(new ResultProblem(exception, "Failed to delete '{0}' at '{1}'", key, file.Path));
        }
        finally
        {
            _gate.Release();
        }
    }

    private IPath Locate(UserStorageKey key) => root / key.Area.ToString() / key.Entry.ToString();

    private static void TryDelete(IPath file)
    {
        try
        {
            File.Delete(file.Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
