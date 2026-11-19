// Lit meshes in the room: the case, panels, knobs, jacks, keys, cables and scope housings.
import pipeline;
import space;

[PassInputs("SpacePass")]
ConstantBuffer<SpacePassData> spacePass;

struct LitInstance
{
    [Instanced]
    Matrix4Rows iWorld;
    /// The surface's sRGB colour, and in a how glossy it is, from 0 (matte) to 1.
    [Instanced]
    float4 iAlbedo;
    /// The sRGB colour it glows with (black for none), and in a how much plastic grain its surface has, from 0 to 1.
    [Instanced]
    float4 iEmissive;
    /// x: how brushed the metal is, from 0 to 1; y: how much it mirrors the room (0 for none, 1 for bare metal); z: the
    /// height, along the part's own y, over which its foot darkens where it meets what it stands on (0 for none); w: 0 to
    /// brush along the part's x, 1 round its y axis.
    [Instanced]
    float4 iFinish;
};

struct VertexOut
{
    float4 position : SV_Position;
    float3 worldPosition;
    float3 localPosition;
    float3 normal;
    float3 tangent;
    float4 albedo;
    float4 emissive;
    float4 finish;
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
VertexOut vertexMain([SharedVertex("LitVertex")] LitVertex v, LitInstance inst)
{
    float4x4 world = inst.iWorld.toMatrix();
    float4 worldPosition = mul(float4(v.position, 1.0), world);

    VertexOut o;
    o.worldPosition = worldPosition.xyz;
    o.localPosition = v.position;
    // The normal matrix (row vectors: the inverse transpose), from the upper 3x3, as the world matrix is affine
    o.normal = mul(inverse3x3((float3x3)world), v.normal);
    // The brushing's direction: along the part's x, or round its y axis
    o.tangent = inst.iFinish.w > 0.5 ? mul(float3(-v.position.z, 0.0, v.position.x), (float3x3)world) : world[0].xyz;
    o.albedo = inst.iAlbedo;
    o.emissive = inst.iEmissive;
    o.finish = inst.iFinish;
    o.position = mul(mul(worldPosition, spacePass.view), spacePass.projection);
    return o;
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    float3 normal = normalize(i.normal);
    float3 albedo = toLinear(i.albedo.rgb);
    float grain = i.emissive.a;
    if (grain > 0.0)
    {
        // Moulded plastic: a fine orange peel that tilts the normal a touch, and a faint speckle in the colour, fixed to
        // the part (its own coordinates, in metres) so it doesn't swim as the part moves
        float3 p = i.localPosition * 900.0;
        float3 tilt = float3(noise(p), noise(p + 17.3), noise(p + 41.1)) - 0.5;
        normal = normalize(normal + (tilt - normal * dot(tilt, normal)) * 0.18 * grain);
        albedo *= 1.0 + (noise(i.localPosition * 3100.0) - 0.5) * 0.07 * grain;
    }

    // Brushed metal: fine streaks across the brushing, faded out where they get finer than a pixel, and a highlight
    // drawn out across them, as the light catches each groove
    float3 p = i.localPosition;
    float across = i.finish.w > 0.5 ? length(p.xz) : p.y;
    float fade = saturate(1.5 - fwidth(across) * 1500.0);
    float3 toEye = normalize(spacePass.cameraPosition - i.worldPosition);
    float streak = 0.5;
    float3 sheen = float3(0.0);
    if (i.finish.x > 0.0)
    {
        float along = i.finish.w > 0.5 ? atan2(p.z, p.x) * 0.02 : p.x;
        // Finer than a pixel, as on a phone, the streaks are left out
        if (fade > 0.01)
        {
            streak = lerp(0.5, noise(float3(along * 30.0, across * 3000.0, 0.0)), fade);
        }

        albedo *= 1.0 + (streak - 0.5) * 0.3 * i.finish.x;
        float3 tangent = normalize(i.tangent + float3(1e-6, 0.0, 0.0));
        float3 h = normalize(normalize(spacePass.keyPosition - i.worldPosition) + toEye);
        float th = dot(tangent, h);
        sheen = spacePass.keyColor * pow(sqrt(saturate(1.0 - th * th)), 90.0) * 0.05 * i.finish.x * (0.6 + 0.8 * streak);
    }

    float3 colour = shade(spacePass, i.worldPosition, normal, albedo, i.albedo.a, float3(0.0)) + sheen + rimLight(normal, toEye);
    if (i.finish.y > 0.0)
    {
        float3 r = reflect(-toEye, normal);
        float fresnel = pow(1.0 - saturate(dot(normal, toEye)), 5.0);
        colour += environment(spacePass, i.worldPosition, r) * lerp(albedo, float3(1.0), fresnel) * i.finish.y * (0.8 + 0.4 * streak);
    }

    if (i.finish.z > 0.0)
    {
        // Darker toward its foot, where little of the room's light reaches
        colour *= lerp(0.35, 1.0, smoothstep(0.0, i.finish.z, p.y));
    }

    colour += toLinear(i.emissive.rgb);
    FragmentOut o;
    o.fragColor = isGlowPass(spacePass) ? float4(i.emissive.rgb, 1.0) : float4(toSrgb(rollOff(colour)), 1.0);
    return o;
}
