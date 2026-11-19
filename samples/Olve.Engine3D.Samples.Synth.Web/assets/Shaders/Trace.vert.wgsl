struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) stroke_0 : vec2<f32>,
    @location(1) phosphor_0 : vec3<f32>,
};

struct vertexInput_0
{
    @location(0) position_1 : vec2<f32>,
    @location(1) stroke_1 : vec2<f32>,
    @location(2) iPhosphor_0 : vec4<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0) -> VertexOut_0
{
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(_S1.position_1, 0.0f, 1.0f);
    o_0.stroke_0 = _S1.stroke_1;
    o_0.phosphor_0 = _S1.iPhosphor_0.xyz;
    return o_0;
}

