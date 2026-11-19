// What the room's shaders share: the vertex of a lit mesh, the pass's camera and lights, and the lighting.
module space;
import pipeline;

/// A vertex of a lit mesh (Shaders.Shared.LitVertex).
public struct LitVertex
{
    [Implements("IWithPosition3D.Position")]
    public float3 position;
    [Implements("IWithNormal3D.Normal")]
    public float3 normal;
};

/// A vertex of the room's still scenery, which is merged into one mesh: its sRGB colour (in a how glossy it is) and its
/// finish ride on each vertex (Shaders.Shared.RoomVertex).
public struct RoomVertex
{
    [Implements("IWithPosition3D.Position")]
    public float3 position;
    [Implements("IWithNormal3D.Normal")]
    public float3 normal;
    public float4 albedo;
    /// x: how much wood grain, running along x; y, z, w: unused.
    public float4 finish;
};

/// The room pass: the camera, soft ambient light from above and below, a warm key light over the seat and two coloured
/// point lights at the sides, each light's colour already times its intensity. The same draws make the glow pass, the
/// light the room's lit parts give off for the bloom: there, with glowOnly.x at 1, each surface gives only the light it
/// emits, black where it emits none, so it still hides what glows behind it.
public struct SpacePassData
{
    public float4x4 view;
    public float4x4 projection;
    public float3 cameraPosition;
    public float3 skyAmbient;
    public float3 groundAmbient;
    public float3 keyPosition;
    public float3 keyColor;
    public float3 light0Position;
    public float3 light0Color;
    public float3 light1Position;
    public float3 light1Color;
    public float4 glowOnly;
};

/// Whether this is the glow pass (see SpacePassData).
public bool isGlowPass(SpacePassData pass) { return pass.glowOnly.x > 0.5; }

/// From the sRGB colours the sample is written in to linear light, and back.
public float3 toLinear(float3 c) { return pow(max(c, float3(0.0)), float3(2.2)); }
public float3 toSrgb(float3 c) { return pow(max(c, float3(0.0)), float3(1.0 / 2.2)); }

/// Bright light rolled off toward white instead of clipping, leaving the darks and mids as they are.
public float3 rollOff(float3 c)
{
    float3 knee = float3(0.75);
    float3 over = max(c - knee, float3(0.0));
    return min(c, knee) + over / (1.0 + over * 2.0);
}

/// Smooth value noise in 3D, from 0 to 1.
float hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

public float noise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x), lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x), lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y),
                f.z);
}

/// Dark varnished wood: soft growth rings across z, warped, in metres of the world.
public float3 woodGrain(float3 p, float3 albedo)
{
    float warp = noise(float3(p.x * 2.5, p.y * 30.0, p.z * 30.0));
    float rings = frac(p.z * 38.0 + warp * 2.6 + noise(p * float3(0.9, 6.0, 6.0)) * 1.2);
    float band = smoothstep(0.0, 0.3, rings) * smoothstep(1.0, 0.6, rings);
    return albedo * (0.72 + 0.4 * band);
}

/// The room as a mirror sees it, for metal: dark, the window's cool city glow ahead and a little up, the warm key light
/// above and behind the seat and the two side lights in their colours. r is the reflected direction.
public float3 environment(SpacePassData pass, float3 position, float3 r)
{
    float3 colour = lerp(pass.groundAmbient, pass.skyAmbient, r.y * 0.5 + 0.5) * 0.7;
    float ahead = -r.z;
    if (ahead > 0.05)
    {
        float2 w = r.xy / ahead;
        float window = smoothstep(1.1, 0.7, abs(w.x)) * smoothstep(-0.25, 0.0, w.y) * smoothstep(1.2, 0.8, w.y);
        colour += float3(0.045, 0.042, 0.075) * window;
    }

    colour += pass.keyColor * pow(saturate(dot(r, normalize(pass.keyPosition - position))), 60.0) * 0.5;
    colour += pass.light0Color * pow(saturate(dot(r, normalize(pass.light0Position - position))), 40.0) * 0.6;
    colour += pass.light1Color * pow(saturate(dot(r, normalize(pass.light1Position - position))), 40.0) * 0.6;
    return colour;
}

/// A faint cool rim on edges turned toward the window behind the synth, so shapes stand off the dark room.
public float3 rimLight(float3 normal, float3 toEye)
{
    float edge = pow(1.0 - saturate(dot(normal, toEye)), 3.0);
    float facing = saturate(dot(normal, normalize(float3(0.0, 0.5, -1.0))) * 0.8 + 0.2);
    return float3(0.05, 0.06, 0.09) * edge * facing;
}

/// What one light adds to a surface if it were white at intensity 1: diffuse light and a soft highlight, fading with
/// distance. It doesn't depend on the light's colour, so it could be baked, leaving only the multiply by the colour.
public float3 whiteLight(float3 lightPosition, float3 position, float3 normal, float3 toEye, float3 albedo, float shine)
{
    float3 toLight = lightPosition - position;
    float distance2 = dot(toLight, toLight);
    float3 l = toLight * rsqrt(max(distance2, 1e-6));
    float falloff = 1.0 / (1.0 + 0.8 * distance2);
    // Wrapped a little past the terminator, so matte plastic turns away from the light softly
    float diffuse = saturate((dot(normal, l) + 0.2) / 1.2);
    float3 h = normalize(l + toEye);
    // A broad, faint sheen for matte surfaces up to a tighter one for glossy, about as bright either way
    float exponent = lerp(12.0, 80.0, shine);
    float highlight = pow(max(dot(normal, h), 0.0), exponent) * shine * (exponent + 8.0) / 160.0 * step(0.0, dot(normal, l));
    return (albedo * diffuse + float3(highlight)) * falloff;
}

/// A surface's colour in linear light: ambient, then each light's white term times its colour, then what it emits.
public float3 shade(SpacePassData pass, float3 position, float3 normal, float3 albedo, float shine, float3 emissive)
{
    float3 toEye = normalize(pass.cameraPosition - position);
    float3 ambient = lerp(pass.groundAmbient, pass.skyAmbient, normal.y * 0.5 + 0.5);
    return albedo * ambient
        + whiteLight(pass.keyPosition, position, normal, toEye, albedo, shine) * pass.keyColor
        + whiteLight(pass.light0Position, position, normal, toEye, albedo, shine) * pass.light0Color
        + whiteLight(pass.light1Position, position, normal, toEye, albedo, shine) * pass.light1Color
        + emissive;
}
