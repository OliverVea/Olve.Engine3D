using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.RenderPasses;

namespace Olve.Engine3D.Rendering;

/// <summary>One frame in the order it is drawn: each pass with its draws.</summary>
public sealed record Frame(IReadOnlyList<FramePass> Passes);

/// <summary>A pass and its draws, in the order they are drawn.</summary>
public sealed record FramePass(PassId Pass, IReadOnlyList<DrawId> Draws);
