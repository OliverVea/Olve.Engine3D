using System.Text.Json.Serialization;
using Olve.Engine3D.Audio.Sfx.Json;
using Olve.Engine3D.Audio.Sfx.Library;

namespace Olve.Engine3D.Audio.Sfx.Commands;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<SfxPanelSummaryJson>))]
[JsonSerializable(typeof(SfxPanelJson))]
[JsonSerializable(typeof(List<SfxSoundSummaryJson>))]
[JsonSerializable(typeof(SfxSoundJson))]
[JsonSerializable(typeof(SoundDocument))]
[JsonSerializable(typeof(List<SfxSlotJson>))]
[JsonSerializable(typeof(SfxDeletionJson))]
[JsonSerializable(typeof(SfxImportJson))]
[JsonSerializable(typeof(SfxFileJson))]
[JsonSerializable(typeof(SfxRenderJson))]
internal sealed partial class SfxCommandJsonContext : JsonSerializerContext;
