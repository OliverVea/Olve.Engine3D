namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed record SfxLibraryDocument(
    int Version,
    IReadOnlyList<PanelDocument> Panels,
    IReadOnlyList<SoundDocument> Sounds,
    IReadOnlyList<BindingDocument> Bindings,
    IReadOnlyList<Guid> SeededPanels,
    IReadOnlyList<Guid> SeededSounds);
