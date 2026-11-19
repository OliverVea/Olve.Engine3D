// Panel ink: flat shapes painted on a surface in the room, each an instanced quad over its bounds that a signed distance
// cuts out, anti-aliased over a pixel: rounded boxes, discs and rings, ring arcs, thick segments and dot-matrix glyphs.
// Shapes are in the surface's design units (x right, y down); paint is lit like its surface, and glowing ink shines.
import pipeline;
import space;

[PassInputs("SpacePass")]
ConstantBuffer<SpacePassData> spacePass;

struct InkInstance
{
    /// Design units to world, as rows: a design unit right, a design unit down, the unit normal, the origin.
    [Instanced]
    Matrix4Rows iSurface;
    /// The shape's bounds in design units: min x, min y, max x, max y.
    [Instanced]
    float4 iBounds;
    /// The kind, then its parameters (see fragmentMain).
    [Instanced]
    float4 iShape;
    [Instanced]
    float4 iExtra;
    /// sRGB, not premultiplied.
    [Instanced]
    float4 iColor;
    /// x: how much the ink glows, from 0 (paint) to 1 (light of its own colour).
    [Instanced]
    float4 iGlow;
};

struct VertexOut
{
    float4 position : SV_Position;
    float2 design;
    float3 worldPosition;
    float3 normal;
    float3 right;
    float3 down;
    float4 bounds;
    float4 shape;
    float4 extra;
    float4 color;
    float glow;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

[shader("vertex")]
VertexOut vertexMain(InkInstance inst, uint vertexId : SV_VulkanVertexID)
{
    // Local array rather than a static const: naga indexes constant arrays only by constants
    float2 corners[6] = { float2(0, 0), float2(1, 0), float2(0, 1), float2(0, 1), float2(1, 0), float2(1, 1) };
    // A little past the shape's bounds on each side, for its anti-aliased edge
    float2 design = lerp(inst.iBounds.xy - 1.0, inst.iBounds.zw + 1.0, corners[vertexId]);
    float4x4 surface = inst.iSurface.toMatrix();
    float4 worldPosition = mul(float4(design, 0.0, 1.0), surface);

    VertexOut o;
    o.position = mul(mul(worldPosition, spacePass.view), spacePass.projection);
    o.design = design;
    o.worldPosition = worldPosition.xyz;
    o.normal = surface[2].xyz;
    o.right = normalize(surface[0].xyz);
    o.down = normalize(surface[1].xyz);
    o.bounds = inst.iBounds;
    o.shape = inst.iShape;
    o.extra = inst.iExtra;
    o.color = inst.iColor;
    o.glow = inst.iGlow.x;
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
    float2 px = i.design;
    float2 centre = (i.bounds.xy + i.bounds.zw) * 0.5;
    float2 halfSize = (i.bounds.zw - i.bounds.xy) * 0.5;
    float2 p = px - centre;
    int kind = int(i.shape.x + 0.5);
    // How many design units one pixel covers here
    float pixel = max(length(fwidth(px)) * 0.7071, 1e-4);

    // Signed distance in design units: negative inside
    float d;
    // How much of a shadow falls here, under a raised glyph's dot or a part standing on the surface
    float shadow = 0.0;
    float3 normal = normalize(i.normal);
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

        // A domed lens, extra.z how strongly: its normal leans out toward its edge
        if (i.extra.z > 0.0)
        {
            float2 q = p / halfSize.x;
            normal = normalize(normal * sqrt(max(1.0 - dot(q, q), 0.05)) + (i.right * q.x + i.down * q.y) * i.extra.z);
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
        // Segment from extra.xy to extra.zw with round ends, shape.y wide; dashed every shape.z along it, when above 0
        d = segment(px, i.extra.xy, i.extra.zw, i.shape.y * 0.5);
        if (i.shape.z > 0.0)
        {
            float2 along = i.extra.zw - i.extra.xy;
            float t = dot(px - i.extra.xy, along) / max(length(along), 1e-4);
            d = max(d, (abs(frac(t / i.shape.z) - 0.5) - 0.25) * i.shape.z);
        }
    }
    else if (kind == 5)
    {
        // A soft dark shadow round the foot of a part on the surface: full inside shape.y of the radius, fading out to the edge
        float r = length(p) / halfSize.x;
        shadow = (1.0 - smoothstep(i.shape.y, 1.0, r)) * i.color.a;
        d = 1e3;
    }
    else
    {
        // Dot-matrix glyph: shape.y and shape.z hold its dots (see glyphDot), each a rounded square in its cell. The ink
        // stands a little off the surface, so each dot casts a faint shadow down and right.
        float2 cell = (i.bounds.zw - i.bounds.xy) / float2(5.0, 7.0);
        float2 local = px - i.bounds.xy;
        float2 index = clamp(floor(local / cell), float2(0.0), float2(4.0, 6.0));
        uint low = uint(i.shape.y + 0.5);
        uint high = uint(i.shape.z + 0.5);
        d = glyphDot(low, high, uint(index.x), uint(index.y))
            ? roundedBox(local - (index + 0.5) * cell, cell * 0.42, min(cell.x, cell.y) * 0.18)
            : 1e3;
        // Too small to see, as on a phone, the shadow is left out
        float2 cast = local - cell * 0.22;
        float2 under = clamp(floor(cast / cell), float2(0.0), float2(4.0, 6.0));
        if (cell.x * 0.22 > pixel * 0.5 && all(cast >= 0.0) && glyphDot(low, high, uint(under.x), uint(under.y)))
        {
            float ds = roundedBox(cast - (under + 0.5) * cell, cell * 0.42, min(cell.x, cell.y) * 0.18);
            shadow = clamp(0.5 - ds / pixel, 0.0, 1.0) * 0.5 * i.color.a;
        }
    }

    float coverage = clamp(0.5 - d / pixel, 0.0, 1.0);
    if (coverage <= 0.0 && shadow <= 0.0)
    {
        discard;
    }

    FragmentOut o;
    if (coverage <= 0.0)
    {
        o.fragColor = isGlowPass(spacePass) ? float4(0.0) : float4(0.0, 0.0, 0.0, shadow);
        return o;
    }

    float3 albedo = toLinear(i.color.rgb);
    // A lens is glossy; it keeps its highlight when it glows
    float shine = i.extra.z > 0.0 && kind == 1 ? 0.85 : 0.15;
    float3 lit = shade(spacePass, i.worldPosition, normal, albedo, shine, float3(0.0));
    float3 colour = lerp(lit, albedo * 1.6, i.glow);
    if (kind == 1 && i.extra.z > 0.0)
    {
        colour += shade(spacePass, i.worldPosition, normal, float3(0.0), shine, float3(0.0)) * i.glow;
    }

    o.fragColor = isGlowPass(spacePass)
        ? float4(i.color.rgb * i.glow, i.color.a * coverage)
        : float4(toSrgb(rollOff(colour)), max(i.color.a * coverage, shadow));
    return o;
}
