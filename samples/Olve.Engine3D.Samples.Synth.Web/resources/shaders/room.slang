// The room's still scenery in one mesh: the wall with its window frame, the floor, the desk and what stands on it. Each
// vertex carries its own colour and finish, so the whole room is one draw. It emits nothing: in the glow it draws black,
// hiding the window's lights behind the wall, the frame and what stands on the desk.
import pipeline;
import space;

[PassInputs("SpacePass")]
ConstantBuffer<SpacePassData> spacePass;

struct RoomInstance
{
    [Instanced]
    Matrix4Rows iWorld;
};

struct VertexOut
{
    float4 position : SV_Position;
    float3 worldPosition;
    float3 normal;
    float4 albedo;
    float4 finish;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

[shader("vertex")]
VertexOut vertexMain([SharedVertex("RoomVertex")] RoomVertex v, RoomInstance inst)
{
    float4x4 world = inst.iWorld.toMatrix();
    float4 worldPosition = mul(float4(v.position, 1.0), world);

    VertexOut o;
    o.worldPosition = worldPosition.xyz;
    // The room is placed without scaling, so its normals turn as its positions do
    o.normal = mul(v.normal, (float3x3)world);
    o.albedo = v.albedo;
    o.finish = v.finish;
    o.position = mul(mul(worldPosition, spacePass.view), spacePass.projection);
    return o;
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    float3 normal = normalize(i.normal);
    float3 albedo = toLinear(i.albedo.rgb);
    if (i.finish.x > 0.0)
    {
        albedo = lerp(albedo, woodGrain(i.worldPosition, albedo), i.finish.x);
    }

    float3 colour = shade(spacePass, i.worldPosition, normal, albedo, i.albedo.a, float3(0.0));
    FragmentOut o;
    o.fragColor = isGlowPass(spacePass) ? float4(0.0, 0.0, 0.0, 1.0) : float4(toSrgb(rollOff(colour)), 1.0);
    return o;
}
