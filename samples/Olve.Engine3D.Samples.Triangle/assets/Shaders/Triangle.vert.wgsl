struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) color_0 : vec3<f32>,
};

struct vertexInput_0
{
    @location(0) iOffset_0 : vec2<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0, @builtin(vertex_index) vertexId_0 : u32) -> VertexOut_0
{
    var corners_0 : array<vec2<f32>, i32(3)> = array<vec2<f32>, i32(3)>( vec2<f32>(-0.5f, -0.5f), vec2<f32>(0.5f, -0.5f), vec2<f32>(0.0f, 0.5f) );
    var colors_0 : array<vec3<f32>, i32(3)> = array<vec3<f32>, i32(3)>( vec3<f32>(1.0f, 0.0f, 0.0f), vec3<f32>(0.0f, 1.0f, 0.0f), vec3<f32>(0.0f, 0.0f, 1.0f) );
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(corners_0[vertexId_0] + _S1.iOffset_0, 0.0f, 1.0f);
    o_0.color_0 = colors_0[vertexId_0];
    return o_0;
}

