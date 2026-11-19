// The bloom's blur: one direction of a separable Gaussian over a glow texture, drawn as one triangle covering the target.
import pipeline;

[GroupInputs]
[PixelType("Rgba8Unorm")]
[Sampler("Filtering")]
Sampler2D uSource;

struct BlurInstance
{
    /// xy: one step along the blur's direction, in the source's texel heights (so a step across can match a texel down
    /// on the screen whatever the source's shape).
    [Instanced]
    float4 iStep;
};

struct VertexOut
{
    float4 position : SV_Position;
    float2 uv;
    float4 step;
};

struct FragmentOut
{
    [PixelType("Rgba8Unorm")]
    float4 fragColor : SV_Target;
};

[shader("vertex")]
VertexOut vertexMain(BlurInstance inst, uint vertexId : SV_VulkanVertexID)
{
    float2 corners[3] = { float2(-1, -1), float2(3, -1), float2(-1, 3) };
    VertexOut o;
    o.position = float4(corners[vertexId], 0.0, 1.0);
    o.uv = float2(corners[vertexId].x * 0.5 + 0.5, 0.5 - corners[vertexId].y * 0.5);
    o.step = inst.iStep;
    return o;
}

[shader("fragment")]
FragmentOut fragmentMain(VertexOut i)
{
    // Thirteen texels, read as seven with the texture's filtering between pairs. The offsets are in texels: they fall
    // between two texels so that one linear read weighs both, and stepping them further apart would skip texels and leave
    // the glow's texel blocks showing
    float weights[4] = { 0.1964825501511404, 0.2969069646728344, 0.0944703978504473, 0.0103813624011480 };
    float offsets[4] = { 0.0, 1.4117647058823530, 3.2941176470588234, 5.1764705882352940 };
    float width;
    float height;
    uSource.GetDimensions(width, height);
    float2 step = i.step.xy / height;
    float3 sum = uSource.Sample(i.uv).rgb * weights[0];
    for (int k = 1; k < 4; k++)
    {
        sum += uSource.Sample(i.uv + step * offsets[k]).rgb * weights[k];
        sum += uSource.Sample(i.uv - step * offsets[k]).rgb * weights[k];
    }

    FragmentOut o;
    o.fragColor = float4(sum, 1.0);
    return o;
}
