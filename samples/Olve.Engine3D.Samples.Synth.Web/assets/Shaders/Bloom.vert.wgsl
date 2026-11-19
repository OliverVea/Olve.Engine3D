struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) uv_0 : vec2<f32>,
    @location(1) strength_0 : vec4<f32>,
};

struct vertexInput_0
{
    @location(0) iStrength_0 : vec4<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0, @builtin(vertex_index) vertexId_0 : u32) -> VertexOut_0
{
    var corners_0 : array<vec2<f32>, i32(3)> = array<vec2<f32>, i32(3)>( vec2<f32>(-1.0f, -1.0f), vec2<f32>(3.0f, -1.0f), vec2<f32>(-1.0f, 3.0f) );
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(corners_0[vertexId_0], 0.0f, 1.0f);
    o_0.uv_0 = vec2<f32>(corners_0[vertexId_0].x * 0.5f + 0.5f, 0.5f - corners_0[vertexId_0].y * 0.5f);
    o_0.strength_0 = _S1.iStrength_0;
    return o_0;
}

