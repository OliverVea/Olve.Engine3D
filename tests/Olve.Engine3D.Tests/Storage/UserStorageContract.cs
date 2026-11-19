using Olve.Engine3D.Storage;
using Olve.Results;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Storage;

public abstract class UserStorageContract
{
    protected static readonly DateTimeOffset Start = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero).AddTicks(1234567);

    protected readonly ManualClock Clock = new(Start);

    protected abstract IUserStorage Storage { get; }

    protected static Id<UserStorageArea> Area(string name) => Id.FromName<UserStorageArea>(name);

    protected static UserStorageKey Key(string area, string name) => new(Area(area), Id.FromName<UserStorageEntry>(name));

    protected async Task<byte[]> Read(UserStorageKey key) =>
        (await Storage.ReadAsync(key, CancellationToken.None)).TryPickProblems(out var problems, out var bytes)
            ? throw new InvalidOperationException(problems.First().ToBriefString())
            : bytes;

    protected async Task Write(UserStorageKey key, params byte[] bytes)
    {
        if ((await Storage.WriteAsync(key, bytes, CancellationToken.None)).TryPickProblems(out var problems))
        {
            throw new InvalidOperationException(problems.First().ToBriefString());
        }
    }

    protected async Task<IReadOnlyList<UserStorageEntry>> List(string area) =>
        (await Storage.ListAsync(Area(area), CancellationToken.None)).TryPickProblems(out var problems, out var entries)
            ? throw new InvalidOperationException(problems.First().ToBriefString())
            : entries;

    private static (UserStorageKey First, UserStorageKey Second) Ordered(UserStorageKey a, UserStorageKey b) =>
        a.Entry.CompareTo(b.Entry) < 0 ? (a, b) : (b, a);

    [Test]
    public async Task Entries_Are_Written_Listed_Overwritten_And_Deleted()
    {
        var (library, settings) = Ordered(Key("audio", "library"), Key("audio", "settings"));
        var save = Key("saves", "library");

        await Assert.That(await List("audio")).IsEmpty();

        await Write(settings, 1, 2, 3);
        Clock.Advance(TimeSpan.FromMinutes(1));
        await Write(library, 4, 5);
        await Write(save, 6);

        await Assert.That(await List("audio")).IsEquivalentTo(
            [new UserStorageEntry(library, Start + TimeSpan.FromMinutes(1)), new UserStorageEntry(settings, Start)],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await Read(settings)).IsEquivalentTo(new byte[] { 1, 2, 3 });

        Clock.Advance(TimeSpan.FromMinutes(1));
        await Write(settings, 7);

        await Assert.That(await Read(settings)).IsEquivalentTo(new byte[] { 7 });
        await Assert.That(await List("audio")).Contains(new UserStorageEntry(settings, Start + TimeSpan.FromMinutes(2)));

        var deleted = await Storage.DeleteAsync(library, CancellationToken.None);
        var deletedAgain = await Storage.DeleteAsync(library, CancellationToken.None);

        await Assert.That(deleted.IsSuccess).IsTrue();
        await Assert.That(deletedAgain.IsNotFound).IsTrue();
        await Assert.That(await List("audio")).IsEquivalentTo([new UserStorageEntry(settings, Start + TimeSpan.FromMinutes(2))]);
        await Assert.That(await Read(save)).IsEquivalentTo(new byte[] { 6 });
    }

    [Test]
    public async Task Reading_A_Missing_Entry_Is_Not_Found()
    {
        var read = await Storage.ReadAsync(Key("audio", "library"), CancellationToken.None);

        await Assert.That(read.TryPickProblem<NotFoundProblem>(out var notFound)).IsTrue();
        await Assert.That(notFound!.ToBriefString()).IsEqualTo($"Nothing is stored at '{Area("audio")}/{Id.FromName<UserStorageEntry>("library")}'");
    }

    [Test]
    public async Task A_Written_Entry_Keeps_Its_Own_Copy_Of_The_Bytes()
    {
        var key = Key("audio", "library");
        var bytes = new byte[] { 1, 2 };

        await Write(key, bytes);
        bytes[0] = 9;
        (await Read(key))[1] = 9;

        await Assert.That(await Read(key)).IsEquivalentTo(new byte[] { 1, 2 });
    }
}

[InheritsTests]
public class InMemoryUserStorageTests : UserStorageContract
{
    public InMemoryUserStorageTests() => Storage = new InMemoryUserStorage(Clock);

    protected override IUserStorage Storage { get; }
}
