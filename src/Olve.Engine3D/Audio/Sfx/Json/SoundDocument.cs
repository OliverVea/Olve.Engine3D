namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed record SoundDocument(Guid Id, string Name, Guid Panel, TimeSpan Length, IReadOnlyList<InputValueDocument> Inputs);
