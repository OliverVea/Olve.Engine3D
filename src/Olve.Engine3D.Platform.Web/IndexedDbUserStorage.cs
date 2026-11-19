using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Olve.Engine3D.Storage;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Platform.Web;

public sealed partial class IndexedDbUserStorage(string databaseName, Uri moduleUri, TimeProvider timeProvider) : IUserStorage
{
    private const string ModuleName = "Olve.Engine3D.Platform.Web.UserStorage";

    private Task<JSObject>? _module;

    public async ValueTask<Result<IReadOnlyList<UserStorageEntry>>> ListAsync(Id<UserStorageArea> area, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            await LoadModuleAsync();
            json = await List(databaseName, area.ToString());
        }
        catch (JSException exception)
        {
            return new ResultProblem(exception, "Failed to list user storage area '{0}'", area);
        }

        using var document = JsonDocument.Parse(json);
        var entries = new List<UserStorageEntry>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (!Id.TryParse<UserStorageEntry>(element.GetProperty("key").GetString() ?? "", out var entry))
            {
                continue;
            }

            entries.Add(new UserStorageEntry(new UserStorageKey(area, entry), DateTimeOffset.FromUnixTimeMilliseconds(element.GetProperty("modified").GetInt64())));
        }

        return entries.OrderBy(entry => entry.Key.Entry).ToList();
    }

    public async ValueTask<Result<byte[]>> ReadAsync(UserStorageKey key, CancellationToken cancellationToken)
    {
        try
        {
            await LoadModuleAsync();
            using var array = await Read(databaseName, key.Area.ToString(), key.Entry.ToString());
            if (array is null)
            {
                return new NotFoundProblem("Nothing is stored at '{0}'", key);
            }

            var bytes = new byte[Length(array)];
            Copy(array, new ArraySegment<byte>(bytes));
            return bytes;
        }
        catch (JSException exception)
        {
            return new ResultProblem(exception, "Failed to read '{0}'", key);
        }
    }

    public async ValueTask<Result> WriteAsync(UserStorageKey key, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        try
        {
            await LoadModuleAsync();
            await Write(databaseName, key.Area.ToString(), key.Entry.ToString(), new ArraySegment<byte>(bytes.ToArray()),
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
            return Result.Success();
        }
        catch (JSException exception)
        {
            return new ResultProblem(exception, "Failed to write '{0}'", key);
        }
    }

    public async ValueTask<DeletionResult> DeleteAsync(UserStorageKey key, CancellationToken cancellationToken)
    {
        try
        {
            await LoadModuleAsync();
            return await Remove(databaseName, key.Area.ToString(), key.Entry.ToString()) ? DeletionResult.Success() : DeletionResult.NotFound();
        }
        catch (JSException exception)
        {
            return DeletionResult.Error(new ResultProblem(exception, "Failed to delete '{0}'", key));
        }
    }

    private Task<JSObject> LoadModuleAsync() =>
        _module ??= JSHost.ImportAsync(ModuleName, moduleUri.ToString());

    [JSImport("list", ModuleName)]
    private static partial Task<string> List(string databaseName, string area);

    [JSImport("read", ModuleName)]
    private static partial Task<JSObject?> Read(string databaseName, string area, string key);

    [JSImport("write", ModuleName)]
    private static partial Task Write(
        string databaseName, string area, string key, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes, double modified);

    [JSImport("remove", ModuleName)]
    private static partial Task<bool> Remove(string databaseName, string area, string key);

    [JSImport("length", ModuleName)]
    private static partial int Length(JSObject bytes);

    [JSImport("copy", ModuleName)]
    private static partial void Copy(JSObject source, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> target);
}
