using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary><see cref="TextureManager.Screen"/>: the surface's texture, readable from a frame's render until the host presents it.</summary>
public sealed class WebGPUScreen(Provider<WebGPUContext> contextProvider, IScreenPresenter presenter, ILogger<WebGPUScreen> logger) : IDisposable
{
    private const int BytesPerPixel = 4;

    // The only sample counts WebGPU allows
    private const int Multisampled = 4;

    private Vector2<int> _configuredSize;
    private Texture _texture;
    private TextureView _view;

    // The depth buffer of passes into the screen
    private Texture _depthTexture;
    private TextureView _depthView;
    private Vector2<int> _depthSize;

    // With multisampling, passes into the screen draw here and the last one resolves into the surface's texture
    private Texture _colorTexture;
    private TextureView _colorView;
    private Vector2<int> _colorSize;

    // Default until a frame acquires the texture
    internal TextureView View => _view;

    internal TextureFormat Format => contextProvider.Value.SurfaceSettings.Format;

    /// <summary>Samples per pixel of passes into the screen: 1 or 4.</summary>
    public uint SampleCount { get; private set; } = 1;

    /// <summary>Sets the samples per pixel before any pass is created; WebGPU allows 1 or 4, so other counts use the nearest.</summary>
    public void SetSamples(int samples)
    {
        SampleCount = samples > 2 ? Multisampled : 1u;
        if (samples is not (0 or 1 or Multisampled))
        {
            logger.LogWarning("WebGPU draws with 1 or 4 samples per pixel; using {SampleCount} instead of {Samples}", SampleCount, samples);
        }
    }

    // Reconfigures the surface first if the window was resized
    internal Result Acquire(Vector2<int> size)
    {
        if (_texture != default)
        {
            return Result.Success();
        }

        var context = contextProvider.Value;
        if (size != _configuredSize && Configure(context, size).TryPickProblems(out var problems))
        {
            return problems.Prepend("Skipped the frame: the surface can't be configured");
        }

        var surfaceTexture = context.Surface.GetCurrentTexture();
        if (surfaceTexture.Status is not (SurfaceGetCurrentTextureStatus.SuccessOptimal or SurfaceGetCurrentTextureStatus.SuccessSuboptimal))
        {
            surfaceTexture.Texture.Dispose();

            // The next frame configures the surface again
            _configuredSize = default;
            return new ResultProblem("Skipped the frame: the surface's texture is unavailable ({0})", surfaceTexture.Status);
        }

        _texture = surfaceTexture.Texture;
        _view = _texture.CreateView();
        return Result.Success();
    }

    // Does nothing when no frame acquired a texture
    public void Present()
    {
        if (_texture == default)
        {
            return;
        }

        presenter.Present(contextProvider.Value.Surface);
        Release();
    }

    // Copies the screen in this frame and completes once a later frame has mapped the copy; returns the bottom row first
    internal async Task<Result<TextureBuffers<TFormat>>> ReadAsync<TFormat>() where TFormat : unmanaged, ITextureFormat
    {
        if (typeof(TFormat) != typeof(ITextureFormat.Rgba8Unorm))
        {
            return new ResultProblem("The screen is Rgba8Unorm, not {0}", typeof(TFormat).Name);
        }

        if (_texture == default)
        {
            return new ResultProblem("No frame has drawn into the screen since it was last presented");
        }

        var context = contextProvider.Value;
        var settings = context.SurfaceSettings;
        if ((settings.Usage & TextureUsage.CopySrc) == 0)
        {
            return new ResultProblem("The surface can't be copied from, so the screen can't be read");
        }

        if (settings.Format is not (TextureFormat.RGBA8Unorm or TextureFormat.BGRA8Unorm))
        {
            return new ResultProblem("The screen can't be read from a {0} surface", settings.Format);
        }

        var (width, height) = (_configuredSize.X, _configuredSize.Y);
        if ((await WebGPUTextureReadback.ReadAsync(context, _texture, width, height, BytesPerPixel, TextureAspect.All))
            .TryPickProblems(out var problems, out var bytes))
        {
            return problems.Prepend("Failed to read the screen");
        }

        if (settings.Format == TextureFormat.BGRA8Unorm)
        {
            for (var i = 0; i < bytes.Length; i += BytesPerPixel)
            {
                (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]);
            }
        }

        var pixels = MemoryMarshal.Cast<byte, TFormat>(bytes).ToArray();
        return new TextureBuffers<TFormat> { Pixels = ImmutableCollectionsMarshal.AsImmutableArray(pixels), Width = width, Height = height };
    }

    public void Dispose()
    {
        if (_texture != default)
        {
            logger.LogWarning("The screen's texture was acquired but never presented; releasing it");
            Release();
        }

        _depthView.Dispose();
        _depthTexture.Dispose();
        _colorView.Dispose();
        _colorTexture.Dispose();
    }

    // Created on first use and again when the screen is resized
    internal TextureView GetDepthView()
    {
        if (_depthView != default && _depthSize == _configuredSize)
        {
            return _depthView;
        }

        _depthView.Dispose();
        _depthTexture.Dispose();
        (_depthTexture, _depthView) = CreateTarget(TextureFormat.Depth32Float);
        _depthSize = _configuredSize;
        return _depthView;
    }

    // The multisampled colour target; created on first use and again when the screen is resized
    internal TextureView GetMultisampledView()
    {
        if (_colorView != default && _colorSize == _configuredSize)
        {
            return _colorView;
        }

        _colorView.Dispose();
        _colorTexture.Dispose();
        (_colorTexture, _colorView) = CreateTarget(Format);
        _colorSize = _configuredSize;
        return _colorView;
    }

    private (Texture Texture, TextureView View) CreateTarget(TextureFormat format)
    {
        var texture = contextProvider.Value.Device.CreateTexture(new TextureDescriptor
        {
            Usage = TextureUsage.RenderAttachment,
            Size = new Extent3D { Width = (uint)_configuredSize.X, Height = (uint)_configuredSize.Y, DepthOrArrayLayers = 1 },
            Format = format,
            SampleCount = SampleCount,
        });
        return (texture, texture.CreateView());
    }

    private Result Configure(WebGPUContext context, Vector2<int> size)
    {
        logger.LogDebug("Configuring the surface to {Width}x{Height}", size.X, size.Y);

        var settings = context.SurfaceSettings;
        var configuration = new SurfaceConfiguration
        {
            Device = context.Device,
            Format = settings.Format,
            Usage = settings.Usage,
            Width = (uint)size.X,
            Height = (uint)size.Y,
            PresentMode = settings.PresentMode,
            AlphaMode = settings.AlphaMode,
        };

        context.PushErrorScope();
        context.Surface.Configure(configuration);
        if (context.PopErrorScope($"configuring the surface to {size.X}x{size.Y}").TryPickProblems(out var problems))
        {
            return problems;
        }

        _configuredSize = size;
        logger.LogInformation("Configured the surface to {Width}x{Height}", size.X, size.Y);
        return Result.Success();
    }

    private void Release()
    {
        _view.Dispose();
        _texture.Dispose();
        _view = default;
        _texture = default;
    }
}
