using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Math;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Textures;
using Olve.Results;
using Olve.Results.TUnit;
using Olve.Utilities.IntegerMath2D;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Rendering;

/// <summary>Textures that follow the screen's size, and passes that draw multisampled into textures.</summary>
public class ScreenSizedTextureTests : IDisposable
{
    private readonly Window _window = new() { ContentAreaSize = new Vector2<int>(1280, 721) };
    private readonly Backend _backend = new();
    private readonly Id<RenderingSegment> _segment = Id.New<RenderingSegment>();
    private readonly TextureManager _textures;
    private readonly PassManager _passes;

    public ScreenSizedTextureTests()
    {
        _textures = new TextureManager(_backend, _window, NullLogger<TextureManager>.Instance);
        _passes = new PassManager(_backend, _textures, NullLogger<PassManager>.Instance);
    }

    public void Dispose()
    {
        _passes.Dispose();
        _textures.Dispose();
    }

    [Test]
    public async Task A_screen_sized_texture_is_a_divisor_th_of_the_screen_rounded_up_and_follows_it_when_it_changes()
    {
        var quarter = await _textures.CreateScreenSized<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>(4, _segment).AssertSuccessAndGetAsync();
        var fixedSize = await _textures.Create<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>(new Size(64, 64), _segment).AssertSuccessAndGetAsync();
        await Assert.That(_backend.Log).IsEquivalentTo([$"create {quarter} 320x181", $"create {fixedSize} 64x64"]);

        await Assert.That(_textures.FollowScreen()).Succeeded();
        await Assert.That(_backend.Log.Count).IsEqualTo(2).Because("the screen hasn't changed");

        _window.ContentAreaSize = new Vector2<int>(1001, 500);
        await Assert.That(_textures.FollowScreen()).Succeeded();
        await Assert.That(_textures.FollowScreen()).Succeeded();
        await Assert.That(_backend.Log.Skip(2)).IsEquivalentTo([$"resize {quarter} 251x125"]);
        await Assert.That(_textures.TryGetSize(quarter, out var size) ? size : default).IsEqualTo(new Size(251, 125));
    }

    [Test]
    public async Task A_texture_can_t_be_a_fraction_of_the_screen_below_one()
    {
        var created = _textures.CreateScreenSized<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>(0, _segment);

        await Assert.That(created.TryPickProblems(out var problems) ? problems.First().ToBriefString() : "")
            .IsEqualTo("A texture can't be a 0th of the screen's size; the divisor must be 1 or more");
    }

    [Test]
    [Arguments(true, true, "")]
    [Arguments(false, false, "")]
    [Arguments(true, false, "A pass's colour and depth attachments must both be multisampled or neither")]
    [Arguments(false, true, "A pass's colour and depth attachments must both be multisampled or neither")]
    public async Task A_pass_draws_multisampled_into_both_its_attachments_or_neither(bool colorSampled, bool depthSampled, string problem)
    {
        var color = await _textures.CreateScreenSized<ITextureFormat.Rgba8Unorm, ITextureUsage.SampledRenderTarget>(1, _segment, colorSampled).AssertSuccessAndGetAsync();
        var depth = await _textures.CreateScreenSized<ITextureFormat.Depth, ITextureUsage.RenderTarget>(1, _segment, depthSampled).AssertSuccessAndGetAsync();

        var pass = _passes.Register(ColorAttachment.Keep(color), DepthAttachment.Clear(depth, 1f), new IPassInputs.None(), priority: 0, _segment);

        await Assert.That(pass.TryPickProblems(out var problems) ? problems.Last().ToBriefString() : "").IsEqualTo(problem);
        await Assert.That(_backend.Log.Where(l => l.StartsWith("create")).All(l => l.EndsWith(" 1280x721"))).IsTrue();
        TextureId[] multisampled = [.. new (TextureId Texture, bool Sampled)[] { (color, colorSampled), (depth, depthSampled) }.Where(t => t.Sampled).Select(t => t.Texture)];
        await Assert.That(_backend.Multisampled).IsEquivalentTo(multisampled);
    }

    private sealed class Window : IWindowInfo
    {
        public Vector2<int> ContentAreaSize { get; set; }
    }

    private sealed class Backend : ITextureBackend, IPassBackend
    {
        public List<string> Log { get; } = [];

        public List<TextureId> Multisampled { get; } = [];

        public Result UploadTexture<TFormat>(TextureId<TFormat, ITextureUsage.Uploaded> textureId, TextureBuffers<TFormat> textureData)
            where TFormat : unmanaged, ITextureFormat => Result.Success();

        public Result CreateTexture<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId, Size textureSize, bool multisampled)
            where TFormat : ITextureFormat
            where TUsage : ITextureUsage.IRenderable
        {
            Log.Add($"create {textureId} {textureSize.Width}x{textureSize.Height}");
            if (multisampled)
            {
                Multisampled.Add(textureId);
            }

            return Result.Success();
        }

        public Result ResizeTexture(TextureId textureId, Size textureSize)
        {
            Log.Add($"resize {textureId} {textureSize.Width}x{textureSize.Height}");
            return Result.Success();
        }

        public DeletionResult DeleteTexture(TextureId textureId) => DeletionResult.Success();

        public ValueTask<Result<TextureBuffers<TFormat>>> ReadTextureAsync<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId)
            where TFormat : unmanaged, ITextureFormat
            where TUsage : ITextureUsage.IRenderable => throw new NotSupportedException();

        public Result CreatePass<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, PassAttachments attachments, TPassInputs inputs)
            where TColor : IColorFormat
            where TPassInputs : IPassInputs => Result.Success();

        public Result SetInputs<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, TPassInputs inputs)
            where TColor : IColorFormat
            where TPassInputs : IPassInputs => Result.Success();

        public DeletionResult DeletePass(PassId passId) => DeletionResult.Success();
    }
}
