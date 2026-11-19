using System.Text;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Graphics.WebGPU;
using Olve.Engine3D.Input;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;

namespace Olve.Engine3D.Platform.Web;

public sealed class WebPlatform(
    WebHostOptions options,
    Provider<WebGPUContext> contextProvider,
    WebGPUScreen screen,
    PlatformEvents platformEvents,
    ILogger<WebPlatform> logger) : IPlatform, ICursorControl
{
    public bool IsLocked { get; private set; }

    /// <summary>Whether the page hides the cursor over the canvas; it asks every frame.</summary>
    public bool IsHidden { get; private set; }

    public void SetHidden(bool hidden) => IsHidden = hidden;

    /// <summary>Pointer lock needs a user gesture in a browser, so locking only moves the pointer to the centre.</summary>
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        if (locked)
        {
            platformEvents.AddAxis(MouseAxis.Position, new Vector2(_canvasSize.X / 2f, _canvasSize.Y / 2f));
        }
    }

    private readonly HashSet<Button> _held = [];
    private readonly HashSet<int> _touches = [];
    private Vector2<int> _canvasSize;
    private bool _initialized;

    public async Task<Result> InitializeAsync()
    {
        screen.SetSamples(options.Samples);

        if ((await CreateContextAsync()).TryPickProblems(out var problems, out var context))
        {
            return problems.Prepend("Error while creating the WebGPU device");
        }

        contextProvider.Set(context);
        _initialized = true;
        platformEvents.AddResize(_canvasSize);
        return Result.Success();
    }

    public void SetCanvasSize(Vector2<int> size)
    {
        if (size == _canvasSize)
        {
            return;
        }

        _canvasSize = size;
        if (_initialized)
        {
            platformEvents.AddResize(size);
        }
    }

    public void ReportKey(string code, bool isDown)
    {
        if (DomInputMapping.ToEngineKey(code) is var key and not Input.Key.Unknown)
        {
            Report(key, isDown);
        }
    }

    public void ReportMouseButton(int button, bool isDown)
    {
        if (DomInputMapping.ToEngineMouseButton(button) is { } mouseButton)
        {
            Report(mouseButton, isDown);
        }
    }

    // Each finger touching, by the browser's pointer id: a pointer of its own from touching to lifting
    public void ReportTouchStart(int touch, Vector2 position)
    {
        var pointer = Pointer.Touch(touch);
        if (!_touches.Add(touch))
        {
            return;
        }

        platformEvents.AddPointer(pointer);
        platformEvents.AddAxis(pointer.Position, position);
        Report(pointer.Primary, isDown: true);
    }

    public void ReportTouchMove(int touch, Vector2 position)
    {
        if (_touches.Contains(touch))
        {
            platformEvents.AddAxis(InputAxis.Touch(touch), position);
        }
    }

    public void ReportTouchEnd(int touch)
    {
        if (!_touches.Remove(touch))
        {
            return;
        }

        var pointer = Pointer.Touch(touch);
        Report(pointer.Primary, isDown: false);
        platformEvents.RemovePointer(pointer);
    }

    public void ReportPointer(Vector2 position) => platformEvents.AddAxis(MouseAxis.Position, position);

    public void ReportScroll(Vector2 notches) => platformEvents.AddAxis(MouseAxis.Scroll, notches);

    public void ReleaseAll()
    {
        foreach (var button in _held)
        {
            platformEvents.AddButtonUp(button);
        }

        logger.LogDebug("Released {Count} held button(s) as the page lost focus", _held.Count);
        _held.Clear();

        foreach (var touch in _touches)
        {
            platformEvents.RemovePointer(Pointer.Touch(touch));
        }

        _touches.Clear();
    }

    private void Report(Button button, bool isDown)
    {
        if (isDown)
        {
            if (_held.Add(button))
            {
                platformEvents.AddButtonDown(button);
            }
        }
        else if (_held.Remove(button))
        {
            platformEvents.AddButtonUp(button);
        }
    }

    private Task<Result<WebGPUContext>> CreateContextAsync()
    {
        var surfaceDescriptor = new SurfaceDescriptor
        {
            EmscriptenSurfaceSourceCanvasHTMLSelector = new EmscriptenSurfaceSourceCanvasHTMLSelector
            {
                Selector = Encoding.UTF8.GetBytes(options.CanvasSelector),
            },
        };

        return WebGPUContext.CreateAsync(surfaceDescriptor, vSync: true, logger);
    }
}
