using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Olve.Engine3D.Platform.Web;
using Olve.Engine3D.Samples.Synth;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Samples.Synth.Space;
using Olve.Engine3D.Samples.Synth.Web;
using Olve.Engine3D.Scenes;

[assembly: SupportedOSPlatform("browser")]

var baseUri = new Uri(JSHost.GlobalThis.GetPropertyAsJSObject("document")?.GetPropertyAsString("baseURI")
                      ?? throw new InvalidOperationException("The page has no document to read its base URI from"));
await JSHost.ImportAsync(WebSynthAudio.ModuleName, new Uri(baseUri, "synth.js").ToString());

WebHost.Start(new WebHostOptions { AppName = "Olve.Engine3D.Samples.Synth", CanvasSelector = "#canvas", Samples = WebSynthAudio.Samples() }, services =>
{
    services.AddSynthSample();
    services.AddSingleton(new SynthOptions(WebSynthAudio.NarrowScreen() ? RackLayout.NarrowOctaves : RackLayout.WideOctaves));
    services.AddSingleton<ISynthAudio, WebSynthAudio>();
    services.AddScoped<ShapeCanvas>();
    services.AddScoped<ScopeScreens>();
    services.AddSingleton(WebSynthAudio.FinishOptions());
    services.AddScoped<RoomFinish>();
    services.AddScoped<RoomScenery>();
    services.AddSceneService<SeatService>(SynthSample.SceneId);
    services.AddScoped<IRackViews>(provider => provider.GetRequiredService<SeatService>());
    services.AddSceneService<SpaceRenderService>(SynthSample.SceneId);
    services.AddSceneService<SynthChecksService>(SynthSample.SceneId);
    services.AddSceneService<SynthInputService>(SynthSample.SceneId);
});
