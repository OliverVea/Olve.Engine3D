// Flat 2D shapes on screen, each an instanced quad over its bounds that a signed distance cuts out, anti-aliased over a
// pixel: rounded boxes, discs and rings, ring arcs, thick segments and dot-matrix glyphs.
import pipeline;

public struct ShapePassData
{
    /// The screen size in pixels.
    public float2 uResolution;
};

[PassInputs("ShapePass")]
ConstantBuffer<ShapePassData> shapePass;

struct ShapeInstance
{
    /// The shape's bounds, in pixels from the top left: min x, min y, max x, max y.
    [Instanced]
    float4 iBounds;
    /// The kind, then its parameters (see fragmentMain).
    [Instanced]
    float4 iShape;
    [Instanced]
    float4 iExtra;
    /// RGBA, not premultiplied.
    [Instanced]
    float4 iColor;
};

struct VertexOut
{
    float4 position : SV_Position;
    float4 bounds;
    float4 shape;
    float4 extra;
    float4 color;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

[shader("vertex")]
VertexOut vertexMain(ShapeInstance inst, uint vertexId : SV_VulkanVertexID)
{
    // Local array rather than a static const: naga indexes constant arrays only by constants
    float2 corners[6] = { float2(0, 0), float2(1, 0), float2(0, 1), float2(0, 1), float2(1, 0), float2(1, 1) };
    // A pixel past the shape's bounds on each side, for its anti-aliased edge
    float2 px = lerp(inst.iBounds.xy - 1.0, inst.iBounds.zw + 1.0, corners[vertexId]);

    VertexOut o;
    o.position = float4(px.x / shapePass.uResolution.x * 2.0 - 1.0, 1.0 - px.y / shapePass.uResolution.y * 2.0, 0.0, 1.0);
    o.bounds = inst.iBounds;
    o.shape = inst.iShape;
    o.extra = inst.iExtra;
    o.color = inst.iColor;
    return o;
}

float roundedBox(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
}

float segment(float2 p, float2 a, float2 b, float halfWidth)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = clamp(dot(pa, ba) / max(dot(ba, ba), 0.0001), 0.0, 1.0);
    return length(pa - ba * h) - halfWidth;
}

// Whether the glyph's dot at column x, row y (5 by 7, top row first) is lit: rows 0-3 are in the low mask, rows 4-6 in the high
bool glyphDot(uint low, uint high, uint x, uint y)
{
    uint bit = y < 4u ? (low >> (y * 5u + x)) : (high >> ((y - 4u) * 5u + x));
    return (bit & 1u) != 0u;
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    float2 px = i.position.xy;
    float2 centre = (i.bounds.xy + i.bounds.zw) * 0.5;
    float2 halfSize = (i.bounds.zw - i.bounds.xy) * 0.5;
    float2 p = px - centre;
    int kind = int(i.shape.x + 0.5);

    // Signed distance in pixels: negative inside
    float d;
    if (kind == 0)
    {
        // Rounded box: shape.y the corner radius, shape.z an outline width (0 fills it)
        d = roundedBox(p, halfSize, i.shape.y);
        if (i.shape.z > 0.0)
        {
            d = abs(d + i.shape.z * 0.5) - i.shape.z * 0.5;
        }
    }
    else if (kind == 1)
    {
        // Disc filling the bounds' width, or a ring with inner radius shape.y
        float r = length(p);
        d = r - halfSize.x;
        if (i.shape.y > 0.0)
        {
            d = max(d, i.shape.y - r);
        }
    }
    else if (kind == 2)
    {
        // Ring arc, outer radius the bounds' half width, inner radius shape.y, clockwise from angle extra.x to extra.y
        // (radians, 0 straight up)
        float r = length(p);
        float ring = max(r - halfSize.x, i.shape.y - r);
        float angle = atan2(p.x, -p.y);
        float middle = (i.extra.x + i.extra.y) * 0.5;
        float halfSpan = (i.extra.y - i.extra.x) * 0.5;
        float offAngle = abs(angle - middle) - halfSpan;
        d = max(ring, offAngle * r);
    }
    else if (kind == 3)
    {
        // Segment from extra.xy to extra.zw with round ends, shape.y wide
        d = segment(px, i.extra.xy, i.extra.zw, i.shape.y * 0.5);
    }
    else
    {
        // Dot-matrix glyph: shape.y and shape.z hold its dots (see glyphDot), each a rounded square in its cell
        float2 cell = (i.bounds.zw - i.bounds.xy) / float2(5.0, 7.0);
        float2 local = px - i.bounds.xy;
        float2 index = clamp(floor(local / cell), float2(0.0), float2(4.0, 6.0));
        if (!glyphDot(uint(i.shape.y + 0.5), uint(i.shape.z + 0.5), uint(index.x), uint(index.y)))
        {
            discard;
        }

        float2 inCell = local - (index + 0.5) * cell;
        d = roundedBox(inCell, cell * 0.42, min(cell.x, cell.y) * 0.18);
    }

    float coverage = clamp(0.5 - d, 0.0, 1.0);
    if (coverage <= 0.0)
    {
        discard;
    }

    FragmentOut o;
    o.fragColor = float4(i.color.rgb, i.color.a * coverage);
    return o;
}
