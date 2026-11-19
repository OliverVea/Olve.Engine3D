using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Assets;
using Olve.Engine3D.Assets.Entities;
using Olve.Paths;
using Olve.Results;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Assets;

public class AssetLoaderTests
{
    private sealed class FakeAssetSource(Dictionary<string, byte[]> assets) : IAssetSource
    {
        public ValueTask<Result<byte[]>> ReadAsync(IPurePath assetPath, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<byte[]>>(assets.TryGetValue(assetPath.Path, out var bytes)
                ? bytes
                : new ResultProblem("No asset at '{0}'", assetPath.Path));
    }

    private static readonly AssetPath<HeightmapData> Heightmap = new("Heightmap", "Terrains/heightmap.terrain");

    private static AssetLoader Create(Dictionary<string, byte[]> assets)
        => new(new FakeAssetSource(assets), NullLogger<AssetLoader>.Instance);

    [Test]
    public async Task Deserializes_What_The_Source_Reads()
    {
        var heightmap = new HeightmapData { Heights = [1, 2, 3, 4], Width = 2, Length = 2, Step = 0.5f };
        var loader = Create(new() { [Heightmap.Path.Path] = MemoryPackSerializer.Serialize(heightmap) });

        var result = await loader.LoadAssetAsync(Heightmap, CancellationToken.None);

        await Assert.That(result).Succeeded();
        await Assert.That(result.TryPickProblems(out _, out var loaded) ? null : loaded!.Heights).IsEquivalentTo(heightmap.Heights);
    }

    [Test]
    public async Task Fails_When_The_Source_Has_No_Asset()
    {
        var loader = Create([]);

        await Assert.That(await loader.LoadAssetAsync(Heightmap, CancellationToken.None)).Failed();
    }

    [Test]
    public async Task Fails_When_The_Bytes_Are_Not_The_Asset()
    {
        var loader = Create(new() { [Heightmap.Path.Path] = [1, 2, 3] });

        await Assert.That(await loader.LoadAssetAsync(Heightmap, CancellationToken.None)).Failed();
    }
}
