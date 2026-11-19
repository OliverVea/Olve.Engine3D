using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Sfx.Json;
using Olve.Engine3D.Storage;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed class SfxLibraryStore(IUserStorage storage, ILogger<SfxLibraryStore> logger)
{
    private static readonly Id<UserStorageArea> Area = Id.FromName<UserStorageArea>("sfx-library");

    public static readonly UserStorageKey Key = new(Area, Id.FromName<UserStorageEntry>("library"));

    private Task<Result> _writes = Task.FromResult(Result.Success());

    public bool CanSave { get; private set; } = true;

    public async Task<SfxLibraryContent> ReadAsync(CancellationToken cancellationToken)
    {
        var read = await storage.ReadAsync(Key, cancellationToken);
        if (read.TryPickProblems(out var problems, out var bytes))
        {
            if (read.TryPickProblem<NotFoundProblem>(out _))
            {
                logger.LogInformation("No SFX library stored yet; starting one");
                return SfxLibraryContent.Empty;
            }

            CanSave = false;
            logger.LogError("Failed to read the SFX library; changes to it won't be saved this session: {Problems}", Describe(problems));
            return SfxLibraryContent.Empty;
        }

        var deserialized = SfxLibraryCodec.Deserialize(bytes);
        if (!deserialized.TryPickProblems(out problems, out var content))
        {
            return content;
        }

        if (deserialized.TryPickProblem<NewerSfxLibraryProblem>(out var newer))
        {
            CanSave = false;
            logger.LogError("The stored SFX library was saved by a newer game, so it's left as it is and changes to it won't be saved this session: {Problem}",
                newer.ToBriefString());
            return SfxLibraryContent.Empty;
        }

        var rejected = new UserStorageKey(Area, Id.New<UserStorageEntry>());
        logger.LogError("The stored SFX library is broken; keeping it at {Key} and starting a new one: {Problems}", rejected, Describe(problems));
        if ((await storage.WriteAsync(rejected, bytes, cancellationToken)).TryPickProblems(out var writeProblems))
        {
            CanSave = false;
            logger.LogError("Failed to keep the broken SFX library; changes to the library won't be saved this session: {Problems}", Describe(writeProblems));
        }

        return SfxLibraryContent.Empty;
    }

    public Task<Result> Flush() => _writes;

    public Task<Result> SaveAsync(SfxLibraryContent content)
    {
        if (!CanSave)
        {
            return Task.FromResult<Result>(new ResultProblem("Changes to the SFX library can't be saved this session; the log says why"));
        }

        _writes = WriteAfter(_writes, SfxLibraryCodec.Serialize(content));
        return _writes;
    }

    private async Task<Result> WriteAfter(Task<Result> previous, byte[] bytes)
    {
        _ = await previous;
        Result written;
        try
        {
            written = await storage.WriteAsync(Key, bytes, CancellationToken.None);
        }
        catch (Exception exception)
        {
            written = new ResultProblem(exception, "The write failed");
        }

        if (written.TryPickProblems(out var problems))
        {
            logger.LogError("Failed to save the SFX library: {Problems}", Describe(problems));
            return problems.Prepend("Failed to save the SFX library");
        }

        return Result.Success();
    }

    private static string Describe(IEnumerable<ResultProblem> problems) =>
        string.Join("; ", problems.Select(problem => problem.ToBriefString()));
}
