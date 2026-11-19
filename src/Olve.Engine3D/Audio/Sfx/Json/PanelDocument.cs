namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed record PanelDocument(
    Guid Id,
    string Name,
    IReadOnlyList<NodeDocument> Nodes,
    IReadOnlyList<ConnectionDocument> Connections,
    IReadOnlyList<InputDocument> Inputs,
    string Output);
