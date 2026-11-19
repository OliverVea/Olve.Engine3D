using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Diagnostics;
using Olve.Engine3D.Scenes;
using Olve.Results;
using Olve.Results.TUnit;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Scenes;

public class SceneSharedServiceTests
{
    private sealed class CountingService : ISceneService
    {
        public int LoadCount { get; private set; }

        public Result Load()
        {
            LoadCount++;
            return Result.Success();
        }
    }

    private sealed class OtherService : ISceneService;

    private sealed class ChildService(CountingService counting) : ISceneService
    {
        public CountingService Counting { get; } = counting;
    }

    private static readonly Id<IScene> RootScene = Id.New<IScene>();
    private static readonly Id<IScene> ChildScene = Id.New<IScene>();
    private static readonly Id<IScene> GrandchildScene = Id.New<IScene>();

    /// <summary>
    /// Root → Child → Grandchild, sharing one scope. Root runs <see cref="CountingService"/>, Child runs <see cref="ChildService"/>
    /// and <see cref="OtherService"/>; <paramref name="register"/> adds the registrations under test.
    /// </summary>
    private static (SceneManager SceneManager, ServiceProvider Provider) BuildSceneManager(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<FaultLogger>();
        services.AddSingleton<SceneScopeAccessor>();
        services.AddSingleton(new SceneDefinition(RootScene, "RootScene"));
        services.AddSingleton(new SceneDefinition(ChildScene, "ChildScene", ParentId: RootScene));
        services.AddSingleton(new SceneDefinition(GrandchildScene, "GrandchildScene", ParentId: ChildScene));
        services.AddSingleton<SceneManager>();
        services.AddSceneService<CountingService>(RootScene);
        services.AddSceneService<ChildService>(ChildScene);
        services.AddSceneService<OtherService>(ChildScene);
        register(services);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<SceneManager>(), provider);
    }

    [Test]
    public async Task Child_Uses_The_Service_Its_Parent_Runs()
    {
        var (sceneManager, provider) = BuildSceneManager(_ => { });

        await Assert.That(sceneManager.LoadAndActivateScene(ChildScene)).Succeeded();

        var scope = provider.GetRequiredService<SceneScopeAccessor>().ActiveScopeProviders.Single();
        await Assert.That(scope.GetRequiredService<ChildService>().Counting.LoadCount).IsEqualTo(1);
    }

    [Test]
    public async Task Child_Registering_A_Service_Its_Parent_Runs_Fails_The_Load()
    {
        var (sceneManager, _) = BuildSceneManager(services => services.AddSceneService<CountingService>(ChildScene));

        await Assert.That(sceneManager.LoadAndActivateScene(ChildScene)).Failed();
    }

    [Test]
    public async Task Grandchild_Registering_A_Service_Its_Grandparent_Runs_Fails_The_Load()
    {
        var (sceneManager, _) = BuildSceneManager(services => services.AddSceneService<CountingService>(GrandchildScene));

        await Assert.That(sceneManager.LoadAndActivateScene(GrandchildScene)).Failed();
    }

    [Test]
    public async Task Every_Duplicate_Is_Reported()
    {
        var (sceneManager, _) = BuildSceneManager(services =>
        {
            services.AddSceneService<CountingService>(GrandchildScene);
            services.AddSceneService<OtherService>(GrandchildScene);
        });

        var result = sceneManager.LoadAndActivateScene(GrandchildScene);

        await Assert.That(result.TryPickProblems(out var problems)).IsTrue();
        var messages = problems!.Select(p => p.ToDebugString()).ToList();
        await Assert.That(messages.Count(m => m.Contains(nameof(CountingService)))).IsEqualTo(1);
        await Assert.That(messages.Count(m => m.Contains(nameof(OtherService)))).IsEqualTo(1);
    }
}
