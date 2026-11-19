using Olve.Engine3D.Audio.Devices;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Assets;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Graphics.WebGPU;
using Olve.Engine3D.Input;
using Olve.Engine3D.Storage;
using Olve.Engine3D.TimeStepping;

namespace Olve.Engine3D.Platform.Web;

public static partial class WebHost
{
    private static WebSession? _session;
    private static WebHostOptions? _options;

    public static void Start(WebHostOptions options, Action<IServiceCollection> addGame)
    {
        if (_session is not null)
        {
            throw new InvalidOperationException("The game has already started");
        }

        var services = new ServiceCollection();
        addGame(services);
        AddWebServices(services, options);

        _options = options;

        _session = new WebSession(GameHost.BuildServiceProvider(services));
    }

    [JSExport]
    public static int Frame(double timestampMilliseconds, int canvasWidth, int canvasHeight) =>
        (int)(_session?.Frame(TimeSpan.FromMilliseconds(timestampMilliseconds), new Vector2<int>(canvasWidth, canvasHeight))
              ?? FrameState.Stopped);

    /// <summary>The most canvas pixels per CSS pixel the page should draw at (<see cref="WebHostOptions.MaxPixelRatio"/>); unlimited before the game starts.</summary>
    [JSExport]
    public static double MaxPixelRatio() => _options?.MaxPixelRatio ?? double.PositiveInfinity;

    [JSExport]
    public static void Key(string code, bool isDown) => _session?.Platform.ReportKey(code, isDown);

    [JSExport]
    public static void MouseButton(int button, bool isDown) => _session?.Platform.ReportMouseButton(button, isDown);

    [JSExport]
    public static void PointerMove(double x, double y) => _session?.Platform.ReportPointer(new Vector2((float)x, (float)y));

    /// <summary>A finger touched the canvas; <paramref name="touch"/> is the browser's pointer id for it.</summary>
    [JSExport]
    public static void TouchStart(int touch, double x, double y) =>
        _session?.Platform.ReportTouchStart(touch, new Vector2((float)x, (float)y));

    [JSExport]
    public static void TouchMove(int touch, double x, double y) =>
        _session?.Platform.ReportTouchMove(touch, new Vector2((float)x, (float)y));

    /// <summary>A finger lifted, or the browser took the touch over.</summary>
    [JSExport]
    public static void TouchEnd(int touch) => _session?.Platform.ReportTouchEnd(touch);

    [JSExport]
    public static void Wheel(double x, double y) => _session?.Platform.ReportScroll(new Vector2((float)x, (float)y));

    /// <summary>Whether the page should hide the cursor over the canvas, as the game draws its own.</summary>
    [JSExport]
    public static bool IsCursorHidden() => _session?.Platform.IsHidden ?? false;

    [JSExport]
    public static void Blur() => _session?.Platform.ReleaseAll();

    private static void AddWebServices(IServiceCollection services, WebHostOptions options)
    {
        services.AddLogging(logging => logging.AddProvider(new BrowserConsoleLoggerProvider()));

        services.AddSingleton(options);
        services.AddSingleton<WebPlatform>();
        services.AddSingleton<IPlatform>(sp => sp.GetRequiredService<WebPlatform>());
        services.AddSingleton<ICursorControl>(sp => sp.GetRequiredService<WebPlatform>());
        services.AddSingleton<WebStepper>();
        services.AddSingleton<ITimeStepper>(sp => sp.GetRequiredService<WebStepper>());
        services.AddSingleton(Pointer.Mouse);

        var baseUri = new Uri(GetDocumentBaseUri());
        services.AddSingleton(provider => ActivatorUtilities.CreateInstance<WebAudioBackend>(
            provider, new Uri(baseUri, "_content/Olve.Engine3D.Platform.Web/webAudio.js")));
        services.AddSingleton<IAudioDeviceBackend>(provider => provider.GetRequiredService<WebAudioBackend>());
        services.AddSingleton<ISoundBackend>(provider => provider.GetRequiredService<WebAudioBackend>());
        services.AddSingleton(new HttpClient { BaseAddress = new Uri(baseUri, "assets/") });
        services.AddSingleton<IAssetSource, FetchAssetSource>();
        services.AddSingleton<IUserStorage>(new IndexedDbUserStorage(
            options.AppName, new Uri(baseUri, "_content/Olve.Engine3D.Platform.Web/userStorage.js"), TimeProvider.System));
        services.AddSingleton<IFileExchange>(new BrowserFileExchange(new Uri(baseUri, "_content/Olve.Engine3D.Platform.Web/fileExchange.js")));

        services.AddWebGPUServices();
        services.AddSingleton<IScreenPresenter, BrowserScreenPresenter>();
    }

    private static string GetDocumentBaseUri() =>
        JSHost.GlobalThis.GetPropertyAsJSObject("document")?.GetPropertyAsString("baseURI")
        ?? throw new InvalidOperationException("The page has no document to read its base URI from");
}
