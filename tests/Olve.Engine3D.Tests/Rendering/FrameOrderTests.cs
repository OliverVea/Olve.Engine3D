using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Results.TUnit;
using Olve.Utilities.IntegerMath2D;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Rendering;

/// <summary>The rendering manager keeps the frame's order between frames: a pass or draw registered or deleted after a frame shows in the next.</summary>
public class FrameOrderTests
{
    [Test]
    public async Task Passes_And_Draws_Registered_Or_Deleted_Between_Frames_Show_In_The_Next()
    {
        var backend = new FakeRenderingBackend();
        var segment = Id.New<RenderingSegment>();
        var textures = new TextureManager(backend, new TestWindowInfo(), NullLogger<TextureManager>.Instance);
        var passes = new PassManager(backend, textures, NullLogger<PassManager>.Instance);
        var shaders = new ShaderManager(backend, NullLogger<ShaderManager>.Instance);
        var meshes = new MeshManager(backend, NullLogger<MeshManager>.Instance);
        var groups = new GroupManager(backend, meshes, NullLogger<GroupManager>.Instance);
        var draws = new DrawManager(backend, passes, shaders, groups, NullLogger<DrawManager>.Instance);
        var rendering = new RenderingManager(backend, passes, draws, textures);

        var texture = await textures.Create<ITextureFormat.Rgba8Unorm, ITextureUsage.RenderTarget>(new Size(4, 4), segment).AssertSuccessAndGetAsync();
        var shader = await shaders.Register(new FakeRenderingBackend.Shader(), segment).AssertSuccessAndGetAsync();
        var mesh = await meshes.Register(new MeshBuffers<IVertexData.None> { Vertices = [default, default, default] }, segment).AssertSuccessAndGetAsync();
        var group = await groups.Register<IVertexData.None, FakeRenderingBackend.NoInstance>(mesh, segment).AssertSuccessAndGetAsync();
        var late = await passes.Register(ColorAttachment.Keep(texture), new IPassInputs.None(), priority: 1, segment).AssertSuccessAndGetAsync();
        var first = await draws.Register(late, shader, group, new IGroupInputs.None(), RenderState.Opaque, key: 0, segment).AssertSuccessAndGetAsync();
        await Assert.That(rendering.Render()).Succeeded();

        var second = await draws.Register(late, shader, group, new IGroupInputs.None(), RenderState.Opaque, key: 0, segment).AssertSuccessAndGetAsync();
        await Assert.That(rendering.Render()).Succeeded();

        var early = await passes.Register(ColorAttachment.Keep(texture), new IPassInputs.None(), priority: 0, segment).AssertSuccessAndGetAsync();
        await Assert.That(rendering.Render()).Succeeded();

        await Assert.That(draws.Delete(first).TryPickProblems(out _)).IsFalse();
        await Assert.That(rendering.Render()).Succeeded();

        // Each frame as its passes, each with its draws
        var frames = backend.Frames.Select(frame => string.Join(" | ", frame.Passes.Select(pass => Describe(pass, early, late, first, second))));
        await Assert.That(string.Join("\n", frames)).IsEqualTo("late: first\nlate: first second\nearly:  | late: first second\nearly:  | late: second");
    }

    private static string Describe(FramePass pass, PassId early, PassId late, DrawId first, DrawId second) =>
        (pass.Pass == early ? "early" : pass.Pass == late ? "late" : "?") + ": "
        + string.Join(" ", pass.Draws.Select(draw => draw == first ? "first" : draw == second ? "second" : "?"));
}
