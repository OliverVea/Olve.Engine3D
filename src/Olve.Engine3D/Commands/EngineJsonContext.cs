using System.Text.Json.Serialization;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;

namespace Olve.Engine3D.Commands;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AudioQuery))]
[JsonSerializable(typeof(AudioOutputJson))]
[JsonSerializable(typeof(AudioDeviceJsonEntry[]))]
[JsonSerializable(typeof(ActionList))]
[JsonSerializable(typeof(RenderingQuery))]
internal sealed partial class EngineJsonContext : JsonSerializerContext;
