using Olve.Utilities.Ids;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Olve.Utilities.IntegerMath2D;
using Olve.Utilities.Lookup;
using Olve.Utilities.Stores;

namespace Olve.Engine3D.Rendering.Textures;

/// <summary>The one source of texture ids.</summary>
public class TextureManager : IDisposable
{
    /// <summary>The window's content area: always registered, sized by the window, and owned by the backend.</summary>
    public static TextureId<ITextureFormat.Rgba8Unorm, ITextureUsage.RenderTarget> Screen { get; } =
        TextureId.New<ITextureFormat.Rgba8Unorm, ITextureUsage.RenderTarget>();

    private readonly ITextureBackend _textureBackend;
    private readonly IWindowInfo _windowInfo;
    private readonly ILogger<TextureManager> _logger;

    private readonly EntityStore<RegisteredTexture, TextureId> _textures = [];

    // The textures that follow the screen's size, for FollowScreen
    private readonly EntityStoreIndex<RegisteredTexture, TextureId, bool> _screenSized;
    private readonly UseCounts<TextureId> _uses = new();
    private readonly SegmentOwnership<TextureId> _owners = new();

    public TextureManager(ITextureBackend textureBackend, IWindowInfo windowInfo, ILogger<TextureManager> logger)
    {
        _textureBackend = textureBackend;
        _windowInfo = windowInfo;
        _logger = logger;
        _screenSized = _textures.CreateIndex(texture => texture is CreatedTexture { ScreenDivisor: not null });
    }

    private abstract record RegisteredTexture(TextureId Id) : IHasId<TextureId>;

    // A texture whose data was uploaded: the data it holds, as TextureBuffers<TFormat>
    private sealed record UploadedTexture(TextureId Id, object Data) : RegisteredTexture(Id);

    // A texture that passes draw into; one that follows the screen is a ScreenDivisor-th of its size each way
    private sealed record CreatedTexture(TextureId Id, Size Size, bool Multisampled, int? ScreenDivisor) : RegisteredTexture(Id);

    public Result<TextureId<TFormat, ITextureUsage.Uploaded>> Register<TFormat>(TextureBuffers<TFormat> textureData, Id<RenderingSegment> segment)
        where TFormat : unmanaged, ITextureFormat
    {
        var textureId = TextureId.New<TFormat, ITextureUsage.Uploaded>();
        _logger.LogDebug("Uploading {Width}x{Height} texture {TextureId}", textureData.Width, textureData.Height, textureId);

        if (Upload(textureId, textureData).TryPickProblems(out var problems))
        {
            return problems;
        }

        _owners.Add(segment, textureId);
        _logger.LogInformation("Uploaded {Width}x{Height} texture {TextureId}", textureData.Width, textureData.Height, textureId);
        return textureId;
    }

    /// <summary>
    /// Creates a texture that passes draw into. A multisampled one is drawn with as many samples per pixel as the screen:
    /// a <see cref="ITextureUsage.SampledRenderTarget"/> keeps the samples apart from the texture and resolves them into it
    /// at the frame's last pass into it, so shaders read it smooth and the samples last only within the frame; a
    /// <see cref="ITextureUsage.RenderTarget"/>, such as the depth buffer of a multisampled pass, is the samples themselves.
    /// A pass's colour and depth attachments are both multisampled or neither.
    /// </summary>
    public Result<TextureId<TFormat, TUsage>> Create<TFormat, TUsage>(Size textureSize, Id<RenderingSegment> segment, bool multisampled = false)
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage.IRenderable
        => CreateTexture<TFormat, TUsage>(textureSize, segment, multisampled, screenDivisor: null);

    private Result<TextureId<TFormat, TUsage>> CreateTexture<TFormat, TUsage>(Size textureSize, Id<RenderingSegment> segment, bool multisampled, int? screenDivisor)
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage.IRenderable
    {
        var textureId = TextureId.New<TFormat, TUsage>();
        _logger.LogDebug("Creating {Width}x{Height} texture {TextureId}", textureSize.Width, textureSize.Height, textureId);

        if (_textureBackend.CreateTexture(textureId, textureSize, multisampled).TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to create {0}x{1} {2} texture",
                textureSize.Width, textureSize.Height, typeof(TFormat).Name);
        }

        _textures.Set(new CreatedTexture(textureId, textureSize, multisampled, screenDivisor));
        _owners.Add(segment, textureId);
        _logger.LogInformation("Created {Width}x{Height} texture {TextureId}", textureSize.Width, textureSize.Height, textureId);
        return textureId;
    }

    /// <summary>
    /// Creates a texture that follows the screen: a <paramref name="divisor"/>th of its size each way, rounded up, and
    /// made again at the screen's new size by <see cref="FollowScreen"/> when the screen's changes, losing what it held.
    /// Otherwise it is as <see cref="Create{TFormat, TUsage}"/> makes it.
    /// </summary>
    public Result<TextureId<TFormat, TUsage>> CreateScreenSized<TFormat, TUsage>(int divisor, Id<RenderingSegment> segment, bool multisampled = false)
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage.IRenderable
    {
        if (divisor < 1)
        {
            return new ResultProblem("A texture can't be a {0}th of the screen's size; the divisor must be 1 or more", divisor);
        }

        return CreateTexture<TFormat, TUsage>(DividedScreenSize(divisor), segment, multisampled, divisor);
    }

    /// <summary>Makes each texture that follows the screen again at the screen's size, where that has changed; before each frame.</summary>
    public Result FollowScreen()
    {
        List<ResultProblem> problems = [];
        foreach (var textureId in _screenSized.GetForKey(true))
        {
            if (!_textures.TryGet(textureId, out var registered) || registered is not CreatedTexture { ScreenDivisor: { } divisor } texture)
            {
                continue;
            }

            var size = DividedScreenSize(divisor);
            if (texture.Size == size)
            {
                continue;
            }

            if (_textureBackend.ResizeTexture(textureId, size).TryPickProblems(out var resizeProblems))
            {
                problems.AddRange(resizeProblems.Prepend("Failed to resize texture '{0}' to {1}x{2}", textureId, size.Width, size.Height));
                continue;
            }

            _logger.LogDebug("Resized texture {TextureId} to {Width}x{Height}", textureId, size.Width, size.Height);
            _textures.Set(texture with { Size = size });
        }

        return problems.Count == 0 ? Result.Success() : new ResultProblemCollection(problems);
    }

    // A divisor-th of the screen's size each way, rounded up, and at least a pixel
    private Size DividedScreenSize(int divisor) => new(
        int.Max(1, (_windowInfo.ContentAreaSize.X + divisor - 1) / divisor),
        int.Max(1, (_windowInfo.ContentAreaSize.Y + divisor - 1) / divisor));

    public Result Update<TFormat>(TextureId<TFormat, ITextureUsage.Uploaded> textureId, TextureBuffers<TFormat> textureData)
        where TFormat : unmanaged, ITextureFormat
    {
        if (!TryGetData(textureId, out var current))
        {
            return new ResultProblem("Texture '{0}' is not registered", textureId);
        }

        if (current.Width != textureData.Width || current.Height != textureData.Height)
        {
            return new ResultProblem("Texture '{0}' is {1}x{2}; it can't be updated with {3}x{4} data",
                textureId, current.Width, current.Height, textureData.Width, textureData.Height);
        }

        return Upload(textureId, textureData);
    }

    public bool TryGetData<TFormat>(
        TextureId<TFormat, ITextureUsage.Uploaded> textureId,
        [MaybeNullWhen(false)] out TextureBuffers<TFormat> textureData)
        where TFormat : unmanaged, ITextureFormat
    {
        var data = _textures.TryGet(textureId, out var registered) && registered is UploadedTexture uploaded ? uploaded.Data : null;
        if (data is TextureBuffers<TFormat> typed)
        {
            textureData = typed;
            return true;
        }

        if (data is not null)
        {
            _logger.LogError("Texture {TextureId} holds {ActualType}, not {RequestedFormat} data",
                textureId, data.GetType().Name, typeof(TFormat).Name);
        }

        textureData = null;
        return false;
    }

    public DeletionResult Delete(TextureId textureId)
    {
        if (textureId == Screen)
        {
            return new ResultProblem("The screen belongs to the window and can't be deleted");
        }

        if (!IsRegistered(textureId))
        {
            return DeletionResult.NotFound();
        }

        if (_uses.Get(textureId) is var uses and > 0)
        {
            return new ResultProblem("Texture '{0}' is still used by {1} consumer(s)", textureId, uses);
        }

        _logger.LogDebug("Deleting texture {TextureId}", textureId);
        _ = _textures.Delete(textureId);
        _owners.Forget(textureId);

        var result = _textureBackend.DeleteTexture(textureId);
        if (!result.TryPickProblems(out _))
        {
            _logger.LogInformation("Deleted texture {TextureId}", textureId);
        }

        return result;
    }

    /// <summary>How many textures are registered.</summary>
    public int Count => _textures.Count;

    /// <summary>Deletes every texture the segment was the last owner of.</summary>
    public Result DeleteSegment(Id<RenderingSegment> segment) => _owners.Release(segment, Delete);

    internal bool TryGetSize(TextureId textureId, out Size textureSize)
    {
        if (textureId == Screen)
        {
            textureSize = new Size(_windowInfo.ContentAreaSize.X, _windowInfo.ContentAreaSize.Y);
            return true;
        }

        if (_textures.TryGet(textureId, out var registered) && registered is CreatedTexture created)
        {
            textureSize = created.Size;
            return true;
        }

        textureSize = default;
        return false;
    }

    /// <summary>Whether passes draw into the texture with the screen's samples per pixel; the screen itself is left out.</summary>
    internal bool IsMultisampled(TextureId textureId) =>
        _textures.TryGet(textureId, out var registered) && registered is CreatedTexture { Multisampled: true };

    internal Result AddUses(IReadOnlyList<TextureId> textureIds)
    {
        if (textureIds.FirstOrDefault(id => !IsRegistered(id)) is { } missing)
        {
            return new ResultProblem("Texture '{0}' is not registered", missing);
        }

        foreach (var textureId in textureIds)
        {
            _uses.Add(textureId);
        }

        return Result.Success();
    }

    internal Result ReleaseUses(IReadOnlyList<TextureId> textureIds)
    {
        var unclaimed = textureIds.Where(id => !_uses.TryRelease(id)).ToArray();
        return unclaimed.Length == 0
            ? Result.Success()
            : new ResultProblem("Textures {0} have no use to release", string.Join(", ", unclaimed));
    }

    public void Dispose()
    {
        TextureId[] textureIds = [.. _textures.List().Select(texture => texture.Id)];
        foreach (var textureId in textureIds)
        {
            _ = _textures.Delete(textureId);
        }

        _screenSized.Dispose();
        _uses.Clear();
        _owners.Clear();

        foreach (var textureId in textureIds)
        {
            var result = _textureBackend.DeleteTexture(textureId);
            if (result.TryPickProblems(out var problems))
            {
                _logger.LogError("Failed to delete texture {TextureId}: {Problems}", textureId, problems);
            }
            else if (result.WasNotFound)
            {
                _logger.LogWarning("Texture {TextureId} was already gone from the texture backend", textureId);
            }
        }
    }

    private bool IsRegistered(TextureId textureId) =>
        textureId == Screen || _textures.Contains(textureId);

    private Result Upload<TFormat>(TextureId<TFormat, ITextureUsage.Uploaded> textureId, TextureBuffers<TFormat> textureData)
        where TFormat : unmanaged, ITextureFormat
    {
        if (_textureBackend.UploadTexture(textureId, textureData).TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to upload {0}x{1} {2} texture '{3}'",
                textureData.Width, textureData.Height, typeof(TFormat).Name, textureId);
        }

        _textures.Set(new UploadedTexture(textureId, textureData));
        return Result.Success();
    }
}
