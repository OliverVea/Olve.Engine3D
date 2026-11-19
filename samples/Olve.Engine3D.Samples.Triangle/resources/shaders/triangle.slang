// A triangle with a colour per corner, built from the vertex index: its mesh has no vertex data.
import pipeline;

struct TriangleInstance
{
    [Instanced]
    float2 iOffset;
};

struct VertexOut
{
    float4 position : SV_Position;
    float3 color;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

[shader("vertex")]
VertexOut vertexMain(TriangleInstance inst, uint vertexId : SV_VulkanVertexID)
{
    // Local arrays rather than static consts: wgpu-native's naga indexes constant arrays only by constants
    float2 corners[3] = { float2(-0.5, -0.5), float2(0.5, -0.5), float2(0.0, 0.5) };
    float3 colors[3] = { float3(1.0, 0.0, 0.0), float3(0.0, 1.0, 0.0), float3(0.0, 0.0, 1.0) };

    VertexOut o;
    o.position = float4(corners[vertexId] + inst.iOffset, 0.0, 1.0);
    o.color = colors[vertexId];
    return o;
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    FragmentOut o;
    o.fragColor = float4(i.color, 1.0);
    return o;
}
