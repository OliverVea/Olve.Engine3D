// A speaker's grille: a dome of woven wire in painted metal. The weave is drawn on the dome's mesh: wires along two
// diagonals, each passing over and under the ones across it, round in section so the light runs along them, with the
// gaps between them see-through to the cone behind. Too fine to see at a distance, it fades to its average.
import pipeline;
import space;

[PassInputs("SpacePass")]
ConstantBuffer<SpacePassData> spacePass;

struct GrilleInstance
{
    [Instanced]
    Matrix4Rows iWorld;
    /// The paint's sRGB colour, and in a how glossy it is, from 0 to 1.
    [Instanced]
    float4 iPaint;
    /// x: the distance from one wire to the next, in the mesh's units; y: the wire's thickness, as a share of that.
    [Instanced]
    float4 iWeave;
};

struct VertexOut
{
    float4 position : SV_Position;
    float3 worldPosition;
    float3 localPosition;
    float3 normal;
    float3 tangent;
    float3 bitangent;
    float4 paint;
    float4 weave;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

float3x3 inverse3x3(float3x3 m)
{
    float3 r0 = cross(m[1], m[2]);
    float3 r1 = cross(m[2], m[0]);
    float3 r2 = cross(m[0], m[1]);
    return transpose(float3x3(r0, r1, r2)) / dot(m[0], r0);
}

[shader("vertex")]
VertexOut vertexMain([SharedVertex("LitVertex")] LitVertex v, GrilleInstance inst)
{
    float4x4 world = inst.iWorld.toMatrix();
    float4 worldPosition = mul(float4(v.position, 1.0), world);
    float3x3 normals = inverse3x3((float3x3)world);

    VertexOut o;
    o.worldPosition = worldPosition.xyz;
    o.localPosition = v.position;
    o.normal = mul(normals, v.normal);
    // The weave's two directions, the diagonals of the dome's base, in the world
    o.tangent = mul(float3(0.70710678, 0.0, 0.70710678), (float3x3)world);
    o.bitangent = mul(float3(0.70710678, 0.0, -0.70710678), (float3x3)world);
    o.paint = inst.iPaint;
    o.weave = inst.iWeave;
    o.position = mul(mul(worldPosition, spacePass.view), spacePass.projection);
    return o;
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    // Across the dome's base, along the two diagonals, in wires
    float2 p = float2(i.localPosition.x + i.localPosition.z, i.localPosition.x - i.localPosition.z) * 0.70710678 / i.weave.x;
    float2 cell = floor(p);
    float2 f = frac(p) - 0.5;
    float halfWire = i.weave.y * 0.5;
    // How far across each wire the point is, from -1 at one side to 1 at the other: a wire runs along each cell's middle
    float2 across = f.yx / halfWire;
    float2 width = fwidth(p) / halfWire;
    float2 onWire = saturate(1.0 - (abs(across) - 1.0) / max(width, float2(1e-4)));
    // Over and under: a wire rises where it crosses one of the other set at an even sum of cells, else sinks
    float parity = fmod(abs(cell.x + cell.y), 2.0) < 0.5 ? 1.0 : -1.0;
    float2 lift = float2(parity, -parity) * cos(f * 3.14159265);
    // The one on top shows where both cover the point
    bool firstOnTop = lift.x >= lift.y;
    float coverage = max(onWire.x, onWire.y);
    float a = firstOnTop ? (onWire.x > 0.0 ? across.x : across.y) : (onWire.y > 0.0 ? across.y : across.x);
    float alongFirst = (firstOnTop && onWire.x > 0.0) || (!firstOnTop && onWire.y <= 0.0) ? 1.0 : 0.0;

    // The wire is round: its normal leans across it, and with its rise and fall along it
    float3 normal = normalize(i.normal);
    float3 sideways = normalize(lerp(i.bitangent, i.tangent, 1.0 - alongFirst));
    float3 along = normalize(lerp(i.tangent, i.bitangent, 1.0 - alongFirst));
    float slope = (firstOnTop ? -parity : parity) * sin((alongFirst > 0.5 ? f.x : f.y) * 3.14159265) * 0.5;
    float3 bent = normalize(normal + sideways * clamp(a, -1.0, 1.0) * 0.9 + along * slope);

    // Too fine to resolve, the weave fades to its average: a uniform dark mesh about as see-through
    float fine = saturate(max(width.x, width.y) * 0.5 - 0.5);
    float average = 1.0 - (1.0 - i.weave.y) * (1.0 - i.weave.y);
    float alpha = lerp(coverage, average, fine);
    bent = normalize(lerp(bent, normal, fine));

    float3 colour = shade(spacePass, i.worldPosition, bent, toLinear(i.paint.rgb), i.paint.a, float3(0.0));
    FragmentOut o;
    o.fragColor = isGlowPass(spacePass) ? float4(0.0, 0.0, 0.0, alpha) : float4(toSrgb(rollOff(colour)), alpha);
    return o;
}
