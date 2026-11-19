using MemoryPack;

namespace Olve.Engine3D.Platform.Desktop.Commands;

/// <summary>A command sent over the command pipe; the game answers with a <see cref="Olve.Engine3D.Commands.CommandResponse"/>.</summary>
[MemoryPackable]
public partial record CommandRequest(string Command);
