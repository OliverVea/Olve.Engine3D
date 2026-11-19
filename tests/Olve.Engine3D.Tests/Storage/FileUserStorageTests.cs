using Olve.Engine3D.Platform.Desktop;
using Olve.Engine3D.Storage;
using Olve.Results;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Storage;

[InheritsTests]
public class FileUserStorageTests : UserStorageContract, IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("user-storage-");

    public FileUserStorageTests() => Storage = new FileUserStorage(Olve.Paths.Path.Create(_root.FullName), Clock);

    protected override IUserStorage Storage { get; }

    public void Dispose() => _root.Delete(recursive: true);

    [Test]
    public async Task Concurrent_Writes_To_One_Key_Leave_One_Whole_Value_And_No_Temporary_Files()
    {
        var key = Key("audio", "library");
        var values = Enumerable.Range(0, 32).Select(i => Enumerable.Repeat((byte)i, 64 * 1024).ToArray()).ToList();

        var writes = await Task.WhenAll(values.Select(value => Task.Run(async () =>
            await Storage.WriteAsync(key, value, CancellationToken.None))));

        var read = await Read(key);
        await Assert.That(writes.All(write => !write.TryPickProblems(out _))).IsTrue();
        await Assert.That(values.Any(value => value.SequenceEqual(read))).IsTrue();
        await Assert.That(new DirectoryInfo(System.IO.Path.Combine(_root.FullName, key.Area.ToString())).GetFiles().Select(file => file.Name))
            .IsEquivalentTo([key.Entry.ToString()]);
    }

    [Test]
    public async Task Only_Files_Named_By_An_Id_Are_Listed()
    {
        var key = Key("audio", "library");
        await Write(key, 1);
        var folder = System.IO.Path.Combine(_root.FullName, key.Area.ToString());
        await File.WriteAllBytesAsync(System.IO.Path.Combine(folder, $"{key.Entry}.0123abcd.tmp"), [2]);
        await File.WriteAllBytesAsync(System.IO.Path.Combine(folder, Guid.Parse(key.Entry.ToString()).ToString("B")), [3]);
        await File.WriteAllBytesAsync(System.IO.Path.Combine(folder, "notes.txt"), [4]);

        await Assert.That(await List("audio")).IsEquivalentTo([new UserStorageEntry(key, Start)]);
        await Assert.That(await Read(key)).IsEquivalentTo(new byte[] { 1 });
    }
}
