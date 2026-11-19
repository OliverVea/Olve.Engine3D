using System.Text.Json.Serialization;

namespace Olve.Engine3D.Audio.Sfx.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true,
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(SfxLibraryDocument))]
internal sealed partial class SfxLibraryJsonContext : JsonSerializerContext;
