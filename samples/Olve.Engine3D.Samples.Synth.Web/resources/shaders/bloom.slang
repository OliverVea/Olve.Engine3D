// The room's finish, drawn as one triangle over the screen: the room as drawn, with the bloom (the blurred glow, and a
// little of the glow itself for a tight core round each light) added, its colours fringing apart toward the far edges as
// the output gets loud, and a soft vignette.
import pipeline;

[GroupInputs]
[PixelType("Rgba8Unorm")]
[Sampler("Filtering")]
Sampler2D uScene;

[GroupInputs]
[PixelType("Rgba8Unorm")]
[Sampler("Filtering")]
Sampler2D uGlow;

[GroupInputs]
[PixelType("Rgba8Unorm")]
[Sampler("Filtering")]
Sampler2D uBlur;

struct BloomInstance
{
    /// x: how strong the blurred glow is, y: how strong the glow's core is, z: how dark the vignette makes the corners,
    /// w: how far apart the colours fringe at the edges, in texture coordinates.
    [Instanced]
    float4 iStrength;
};

struct VertexOut
{
    float4 position : SV_Position;
    float2 uv;
    float4 strength;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

[shader("vertex")]
VertexOut vertexMain(BloomInstance inst, uint vertexId : SV_VulkanVertexID)
{
    float2 corners[3] = { float2(-1, -1), float2(3, -1), float2(-1, 3) };
    VertexOut o;
    o.position = float4(corners[vertexId], 0.0, 1.0);
    o.uv = float2(corners[vertexId].x * 0.5 + 0.5, 0.5 - corners[vertexId].y * 0.5);
    o.strength = inst.iStrength;
    return o;
}

// A small glow texture read smoothly onto the screen: a cubic B-spline over its texels, from four linear reads. A plain
// linear read lays each texel out as a flat-topped square with creased edges, and at a quarter of the screen those
// squares show as blocks round every light
float3 smoothly(Sampler2D source, float2 uv)
{
    float width;
    float height;
    source.GetDimensions(width, height);
    float2 size = float2(width, height);
    float2 texel = uv * size - 0.5;
    float2 cell = floor(texel);
    float2 f = texel - cell;
    // The B-spline's four weights each way, paired into two linear reads
    float2 w0 = (1.0 - f) * (1.0 - f) * (1.0 - f) / 6.0;
    float2 w1 = (4.0 - 6.0 * f * f + 3.0 * f * f * f) / 6.0;
    float2 w3 = f * f * f / 6.0;
    float2 w2 = 1.0 - w0 - w1 - w3;
    float2 g0 = w0 + w1;
    float2 g1 = w2 + w3;
    float2 at0 = (cell - 0.5 + w1 / g0) / size;
    float2 at1 = (cell + 1.5 + w3 / g1) / size;
    return (source.Sample(float2(at0.x, at0.y)).rgb * g0.x + source.Sample(float2(at1.x, at0.y)).rgb * g1.x) * g0.y
        + (source.Sample(float2(at0.x, at1.y)).rgb * g0.x + source.Sample(float2(at1.x, at1.y)).rgb * g1.x) * g1.y;
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    float2 fromMiddle = i.uv - 0.5;
    float edge = dot(fromMiddle, fromMiddle) * 2.0;
    // Red pushed out and blue pulled in, more toward the edges; the bloom is soft already, so only the room fringes
    float2 fringe = fromMiddle * edge * i.strength.w;
    float3 room = float3(uScene.Sample(i.uv + fringe).r, uScene.Sample(i.uv).g, uScene.Sample(i.uv - fringe).b);
    float3 light = room + smoothly(uBlur, i.uv) * i.strength.x + smoothly(uGlow, i.uv) * i.strength.y;
    float vignette = 1.0 - i.strength.z * smoothstep(0.15, 0.95, edge);
    FragmentOut o;
    o.fragColor = float4(min(light * vignette, float3(1.0)), 1.0);
    return o;
}
