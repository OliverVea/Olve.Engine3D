// The night outside the window: a city far off under a hazy sky, in three rows of towers, the nearer taller and darker,
// a few of their windows lit and a red light blinking on the tallest; drawn from the direction the eye looks through
// each point of the glass, so the city stays far away. Over it the glass gives back a faint tint of the room's two
// lights. Only the lit windows and the red lights glow.
import pipeline;
import space;

[PassInputs("SpacePass")]
ConstantBuffer<SpacePassData> spacePass;

struct WindowInstance
{
    [Instanced]
    Matrix4Rows iWorld;
    /// x: the time in seconds, for the blinking lights; y: the glass's half width in metres.
    [Instanced]
    float4 iView;
};

struct VertexOut
{
    float4 position : SV_Position;
    float3 worldPosition;
    float3 localPosition;
    float4 view;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

[shader("vertex")]
VertexOut vertexMain([SharedVertex("LitVertex")] LitVertex v, WindowInstance inst)
{
    float4x4 world = inst.iWorld.toMatrix();
    float4 worldPosition = mul(float4(v.position, 1.0), world);

    VertexOut o;
    o.worldPosition = worldPosition.xyz;
    o.localPosition = v.position;
    o.view = inst.iView;
    o.position = mul(mul(worldPosition, spacePass.view), spacePass.projection);
    return o;
}

float hash1(float n) { return frac(sin(n * 12.9898) * 43758.5453); }
float hash2(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

/// Where the towers stand on the horizon, as the tangent of the angle below straight ahead.
static const float Horizon = -0.07;

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    float3 d = normalize(i.worldPosition - spacePass.cameraPosition);
    float across = atan2(d.x, -d.z);
    float up = d.y / max(length(d.xz), 1e-3);
    // How much angle a pixel spans, for fading the windows to their average where they get too fine
    float pixel = max(fwidth(across), fwidth(up));

    // A deep blue sky over a low, warm-violet haze of the city's light
    float3 sky = lerp(float3(0.026, 0.021, 0.036), float3(0.0025, 0.003, 0.008), smoothstep(Horizon - 0.02, Horizon + 0.3, up));
    float3 colour = sky;
    float3 glow = float3(0.0);

    // Three rows of towers, far to near: more and narrower far off, lost in the haze
    [unroll]
    for (int row = 0; row < 3; row++)
    {
        float k = float(row);
        float columns = lerp(70.0, 26.0, k / 2.0);
        float x = across * columns + k * 17.3;
        float id = floor(x) + k * 101.0;
        float f = frac(x);
        float tall = hash1(id);
        float top = Horizon + (0.012 + 0.05 * tall * tall + 0.012 * k) * (0.7 + 0.5 * k);
        // Gaps between some towers, and setbacks at the sides of others
        float width = 0.55 + 0.4 * hash1(id + 7.1);
        bool inside = up < top && abs(f - 0.5) < width * 0.5 && hash1(id + 3.3) > 0.12;
        // A red light blinking slowly on top of the tallest towers
        if (tall > 0.86)
        {
            float2 beacon = float2((f - 0.5) / columns, up - top - 0.003);
            float blink = smoothstep(0.4, 1.0, sin(i.view.x * 2.2 + id) * 0.5 + 0.5);
            glow += float3(1.0, 0.08, 0.05) * blink * 0.9 * saturate(1.0 - length(beacon) / 0.0025);
        }

        if (!inside)
        {
            continue;
        }

        float3 wall = lerp(float3(0.017, 0.016, 0.026), float3(0.005, 0.0055, 0.008), k / 2.0);
        colour = wall;
        glow = float3(0.0);

        // Windows: a grid on the tower's face, a few lit, warm or cool, dimmer far off
        float2 cell = float2((f - 0.5) / width * lerp(5.0, 7.0, hash1(id + 1.7)), (top - up) * columns * 2.2);
        float2 at = floor(cell);
        float2 inCell = frac(cell);
        float pane = step(0.25, inCell.x) * step(inCell.x, 0.75) * step(0.3, inCell.y) * step(inCell.y, 0.75);
        float lit = step(hash2(at + id * 3.1), lerp(0.05, 0.14, hash1(id + 5.9)));
        float3 light = lerp(float3(1.0, 0.62, 0.3), float3(0.55, 0.7, 1.0), step(0.7, hash2(at + id)));
        // Too fine to see apart, the panes fade to their average
        float fine = saturate(1.6 - pixel * columns * 12.0);
        float shown = lerp(0.13 * lerp(0.05, 0.14, hash1(id + 5.9)) * 3.0, pane * lit, fine);
        glow += light * shown * lerp(0.14, 0.3, k / 2.0);

    }

    // The glass: a faint sheen of the room's lights, each on its own side
    float side = clamp(i.localPosition.x / max(i.view.y, 1e-3), -1.0, 1.0);
    float3 sheen = (spacePass.light0Color * saturate(0.6 - side) + spacePass.light1Color * saturate(0.6 + side)) * 0.004
        + spacePass.keyColor * 0.0006;

    FragmentOut o;
    o.fragColor = isGlowPass(spacePass) ? float4(glow, 1.0) : float4(toSrgb(rollOff(colour + glow + sheen)), 1.0);
    return o;
}
