using System.Runtime.Versioning;
using Olve.Engine3D.Platform.Web;
using Olve.Engine3D.Samples.Triangle;

[assembly: SupportedOSPlatform("browser")]

WebHost.Start(new WebHostOptions { AppName = "Olve.Engine3D.Samples.Triangle", CanvasSelector = "#canvas", Samples = 4 }, services => services.AddTriangleSample());
