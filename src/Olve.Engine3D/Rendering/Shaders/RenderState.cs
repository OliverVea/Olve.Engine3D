namespace Olve.Engine3D.Rendering.Shaders;

/// <summary>How a draw assembles, blends and tests depth: its pipeline state.</summary>
public readonly record struct RenderState(
    BlendMode Blend = BlendMode.None,
    bool DepthWrite = true,
    bool DepthTest = true,
    Topology Topology = Topology.TriangleList)
{
    public static readonly RenderState Opaque = new(BlendMode.None, true);
    public static readonly RenderState AlphaBlend = new(BlendMode.Alpha, true);
    public static readonly RenderState AlphaBlendNoDepth = new(BlendMode.Alpha, false, false);

    public bool IsTransparent => Blend != BlendMode.None;
}
