// A scope's trace, drawn into its screen's texture, which keeps what was there: first a dark veil over the last frame, so
// the old trace fades like phosphor, then the new trace's strokes. Positions are in clip space.
import pipeline;

struct TraceVertex
{
    [Implements("IWithPosition2D.Position")]
    float2 position;
    /// x: across the stroke, from -1 at one edge to 1 at the other, or 2 for the veil; y: how bright, or for the veil how opaque.
    float2 stroke;
};

struct TraceInstance
{
    /// The phosphor's sRGB colour.
    [Instanced]
    float4 iPhosphor;
};

struct VertexOut
{
    float4 position : SV_Position;
    float2 stroke;
    float3 phosphor;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

[shader("vertex")]
VertexOut vertexMain(TraceVertex v, TraceInstance inst)
{
    VertexOut o;
    o.position = float4(v.position, 0.0, 1.0);
    o.stroke = v.stroke;
    o.phosphor = inst.iPhosphor.rgb;
    return o;
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    FragmentOut o;
    if (i.stroke.x > 1.5)
    {
        o.fragColor = float4(0.0, 0.0, 0.0, i.stroke.y);
        return o;
    }

    // Brightest along the middle of the stroke, fading to nothing at its edges
    float across = i.stroke.x;
    float alpha = i.stroke.y * exp(-3.5 * across * across) * (1.0 - smoothstep(0.85, 1.0, abs(across)));
    o.fragColor = float4(i.phosphor, alpha);
    return o;
}
