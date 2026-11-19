using System.Runtime.InteropServices;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Utilities;
using Olve.Utilities.IntegerMath2D;
using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>
/// Textures as 2D textures with one mip level; uploaded rows keep their order, so texture coordinates start at the bottom
/// row. A multisampled texture drawn into has the screen's samples per pixel, kept apart from it if shaders read it.
/// </summary>
internal sealed class WebGPUTextureBackend(Provider<WebGPUContext> contextProvider, WebGPUScreen screen, WebGPUResources resources)
    : ITextureBackend
{
    public Result UploadTexture<TFormat>(TextureId<TFormat, ITextureUsage.Uploaded> textureId, TextureBuffers<TFormat> textureData)
        where TFormat : unmanaged, ITextureFormat
    {
        if (textureData.Pixels.Length != textureData.Width * textureData.Height)
        {
            return new ResultProblem("{0} pixels don't fill a {1}x{2} texture", textureData.Pixels.Length, textureData.Width, textureData.Height);
        }

        var size = new Size(textureData.Width, textureData.Height);
        if (resources.Textures.TryGetValue(textureId, out var texture))
        {
            if (texture.Size != size)
            {
                return new ResultProblem("Texture '{0}' is {1}x{2}, not {3}x{4}", textureId,
                    texture.Size.Width, texture.Size.Height, size.Width, size.Height);
            }
        }
        else
        {
            if (Create(textureId, size, TextureUsage.TextureBinding | TextureUsage.CopyDst).TryPickProblems(out var problems, out texture))
            {
                return problems;
            }

            resources.Textures[textureId] = texture;
        }

        var destination = new TexelCopyTextureInfo { Texture = texture.Texture };
        var layout = new TexelCopyBufferLayout
        {
            BytesPerRow = (uint)(size.Width * texture.Formats.BytesPerPixel),
            RowsPerImage = (uint)size.Height,
        };
        var extent = new Extent3D { Width = (uint)size.Width, Height = (uint)size.Height };
        contextProvider.Value.Queue.WriteTexture(destination, textureData.Pixels.AsSpan(), layout, extent);
        return Result.Success();
    }

    public Result CreateTexture<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId, Size textureSize, bool multisampled)
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage.IRenderable
    {
        if (resources.Textures.ContainsKey(textureId))
        {
            return new ResultProblem("Texture '{0}' already exists", textureId);
        }

        var drawn = new DrawnTexture(multisampled ? screen.SampleCount : 1, typeof(ITextureUsage.ISampleable).IsAssignableFrom(typeof(TUsage)));
        _drawn[textureId] = drawn;
        return CreateDrawn(textureId, textureSize, drawn, generation: 0)
            .IfSucceeded(texture => resources.Textures[textureId] = texture)
            .ToEmptyResult();
    }

    public Result ResizeTexture(TextureId textureId, Size textureSize)
    {
        if (!_drawn.TryGetValue(textureId, out var drawn) || !resources.Textures.TryGetValue(textureId, out var old))
        {
            return new ResultProblem("Texture '{0}' isn't a texture drawn into", textureId);
        }

        if (CreateDrawn(textureId, textureSize, drawn, old.Generation + 1).TryPickProblems(out var problems, out var texture))
        {
            return problems;
        }

        old.Release();
        resources.Textures[textureId] = texture;
        return Result.Success();
    }

    // How a texture drawn into was created, to make it again at another size
    private sealed record DrawnTexture(uint SampleCount, bool Sampled);

    private readonly Dictionary<TextureId, DrawnTexture> _drawn = [];

    private Result<WgpuTexture> CreateDrawn(TextureId textureId, Size size, DrawnTexture drawn, int generation)
    {
        // A texture drawn into once a sample a pixel can be read by shaders and copied back. Multisampled, one that shaders
        // read gets its samples apart and stays single-sampled itself; one they don't read is the samples
        if (drawn.SampleCount == 1 || drawn.Sampled)
        {
            var usage = TextureUsage.RenderAttachment | TextureUsage.TextureBinding | TextureUsage.CopySrc;
            if (Create(textureId, size, usage).TryPickProblems(out var problems, out var texture))
            {
                return problems;
            }

            if (drawn.SampleCount == 1)
            {
                return new WgpuTexture(texture.Texture, texture.View, size, texture.Formats, generation: generation);
            }

            if (Create(textureId, size, TextureUsage.RenderAttachment, drawn.SampleCount).TryPickProblems(out problems, out var samples))
            {
                texture.Release();
                return problems;
            }

            return new WgpuTexture(texture.Texture, texture.View, size, texture.Formats, drawn.SampleCount,
                new WgpuSamples(samples.Texture, samples.View), generation);
        }

        if (Create(textureId, size, TextureUsage.RenderAttachment, drawn.SampleCount).TryPickProblems(out var sampleProblems, out var sampled))
        {
            return sampleProblems;
        }

        return new WgpuTexture(sampled.Texture, sampled.View, size, sampled.Formats, drawn.SampleCount, generation: generation);
    }

    public DeletionResult DeleteTexture(TextureId textureId)
    {
        _drawn.Remove(textureId);
        if (!resources.Textures.Remove(textureId, out var texture))
        {
            return DeletionResult.NotFound();
        }

        texture.Release();
        return DeletionResult.Success();
    }

    public ValueTask<Result<TextureBuffers<TFormat>>> ReadTextureAsync<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId)
        where TFormat : unmanaged, ITextureFormat
        where TUsage : ITextureUsage.IRenderable
        => new(textureId == TextureManager.Screen ? screen.ReadAsync<TFormat>() : ReadAsync<TFormat>(textureId));

    // Textures drawn into start at the top row; the rows come back bottom first, like every texture read
    private async Task<Result<TextureBuffers<TFormat>>> ReadAsync<TFormat>(TextureId textureId)
        where TFormat : unmanaged, ITextureFormat
    {
        if (!resources.Textures.TryGetValue(textureId, out var texture))
        {
            return new ResultProblem("Texture '{0}' has no WebGPU texture", textureId);
        }

        if (texture is { SampleCount: > 1, Samples: null })
        {
            return new ResultProblem("Texture '{0}' is multisampled and isn't resolved, so it can't be read back", textureId);
        }

        var (width, height) = (texture.Size.Width, texture.Size.Height);
        var aspect = texture.Formats.IsDepth ? TextureAspect.DepthOnly : TextureAspect.All;
        var read = WebGPUTextureReadback.ReadAsync(contextProvider.Value, texture.Texture, width, height, texture.Formats.BytesPerPixel, aspect);
        if ((await read).TryPickProblems(out var problems, out var bytes))
        {
            return problems.Prepend("Failed to read texture '{0}'", textureId);
        }

        var pixels = MemoryMarshal.Cast<byte, TFormat>(bytes).ToArray();
        return new TextureBuffers<TFormat> { Pixels = ImmutableCollectionsMarshal.AsImmutableArray(pixels), Width = width, Height = height };
    }

    private Result<WgpuTexture> Create(TextureId textureId, Size size, TextureUsage usage, uint sampleCount = 1)
    {
        if (WebGPUTextureFormats.Get(textureId.Format).TryPickProblems(out var problems, out var formats))
        {
            return problems;
        }

        var context = contextProvider.Value;
        var descriptor = new TextureDescriptor
        {
            Usage = usage,
            Size = new Extent3D { Width = (uint)size.Width, Height = (uint)size.Height, DepthOrArrayLayers = 1 },
            Format = formats.Format,
            SampleCount = sampleCount,
        };

        context.PushErrorScope();
        var texture = context.Device.CreateTexture(descriptor);
        if (context.PopErrorScope($"creating texture '{textureId}'").TryPickProblems(out problems))
        {
            texture.Dispose();
            return problems;
        }

        return new WgpuTexture(texture, texture.CreateView(), size, formats);
    }
}
