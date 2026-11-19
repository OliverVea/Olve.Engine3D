using MemoryPack;

namespace Olve.Engine3D.Commands;

/// <summary>The outcome of a queued command, sent back to whichever command source queued it.</summary>
[MemoryPackable]
public partial record CommandResponse(bool Success, string Output, string[] Errors);
