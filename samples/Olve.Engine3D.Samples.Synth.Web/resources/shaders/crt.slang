// A scope's CRT: its bulging glass, which shows the trace texture through a slightly curved, vignetted picture with faint
// scanlines and a graticule, over a dark, glossy face lit like the rest of the room; and the soft halo of its glow, a
// quad a little in front of the screen.
import pipeline;
import space;

[PassInputs("SpacePass")]
ConstantBuffer<SpacePassData> spacePass;

[GroupInputs]
[PixelType("Rgba8Unorm")]
[Sampler("Filtering")]
Sampler2D uTrace;

struct CrtInstance
{
    [Instanced]
    Matrix4Rows iWorld;
    /// x, y: the screen's width and height in metres; z: 0 for the glass, 1 for the halo; w: how bright the halo is.
    [Instanced]
    float4 iScreen;
    /// The phosphor's sRGB colour.
    [Instanced]
    float4 iPhosphor;
};

struct VertexOut
{
    float4 position : SV_Position;
    float3 worldPosition;
    float3 localPosition;
    float3 normal;
    float4 screen;
    float3 phosphor;
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
VertexOut vertexMain([SharedVertex("LitVertex")] LitVertex v, CrtInstance inst)
{
    float4x4 world = inst.iWorld.toMatrix();
    float4 worldPosition = mul(float4(v.position, 1.0), world);

    VertexOut o;
    o.worldPosition = worldPosition.xyz;
    o.localPosition = v.position;
    o.normal = mul(inverse3x3((float3x3)world), v.normal);
    o.screen = inst.iScreen;
    o.phosphor = inst.iPhosphor.rgb;
    o.position = mul(mul(worldPosition, spacePass.view), spacePass.projection);
    return o;
}

// How far a point is outside a centred box, 0 inside
float outside(float2 p, float2 halfSize)
{
    return length(max(abs(p) - halfSize, float2(0.0)));
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    float2 size = i.screen.xy;
    float2 local = i.localPosition.xy;
    float3 phosphor = toLinear(i.phosphor);
    // The picture: the texture seen through glass that bends it a little toward the corners
    float2 centred = local / size;
    float2 bent = centred * (1.0 + 0.09 * dot(centred, centred));
    float2 uv = float2(bent.x + 0.5, 0.5 - bent.y);
    float inside = 1.0 - smoothstep(0.47, 0.5, max(abs(bent.x), abs(bent.y)));

    // Rgba8 fades leave a few counts behind; they stay dark
    float3 trace = max(uTrace.Sample(uv).rgb - float3(3.0 / 255.0), float3(0.0));
    float scanline = 0.86 + 0.14 * sin(uv.y * 3.14159265 * 2.0 * 110.0);
    float vignette = 1.0 - smoothstep(0.25, 0.75, length(bent * float2(1.0, 1.25)));

    // Ten divisions across and eight up, the middle lines a little brighter
    float2 cells = uv * float2(10.0, 8.0);
    float2 toLine = abs(frac(cells + 0.5) - 0.5) / fwidth(cells);
    float line = 1.0 - saturate(min(toLine.x, toLine.y) - 0.5);
    float2 toMiddle = abs(uv - 0.5) / fwidth(uv);
    float middle = 1.0 - saturate(min(toMiddle.x, toMiddle.y) - 0.5);
    float graticule = max(line * 0.035, middle * 0.06);

    float3 glow = (toLinear(trace) * 1.8 * scanline + phosphor * (graticule + 0.006)) * vignette * inside;
    float3 colour = shade(spacePass, i.worldPosition, normalize(i.normal), float3(0.004, 0.006, 0.005), 0.95, glow);

    // Derivatives and the texture are read above for both, as WGSL wants them in uniform control flow
    FragmentOut o;
    if (i.screen.z > 0.5)
    {
        // The halo: brightest at the screen's edge, gone a few centimetres out
        float d = outside(local, size * 0.42);
        float halo = exp(-d * 90.0) * i.screen.w;
        o.fragColor = float4(toSrgb(phosphor), halo * (isGlowPass(spacePass) ? 0.12 : 0.35));
        return o;
    }

    // The glow pass takes the trace alone: the graticule and the glass stay unbloomed
    float3 traceGlow = toLinear(trace) * vignette * inside;
    o.fragColor = isGlowPass(spacePass) ? float4(toSrgb(traceGlow), 1.0) : float4(toSrgb(rollOff(colour)), 1.0);
    return o;
}
