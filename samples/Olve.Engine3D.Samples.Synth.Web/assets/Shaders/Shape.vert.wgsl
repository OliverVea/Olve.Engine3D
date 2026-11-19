struct ShapePassData_std140_0
{
    @align(16) uResolution_0 : vec2<f32>,
};

@binding(0) @group(0) var<uniform> shapePass_0 : ShapePassData_std140_0;
struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) bounds_0 : vec4<f32>,
    @location(1) shape_0 : vec4<f32>,
    @location(2) extra_0 : vec4<f32>,
    @location(3) color_0 : vec4<f32>,
};

struct vertexInput_0
{
    @location(0) iBounds_0 : vec4<f32>,
    @location(1) iShape_0 : vec4<f32>,
    @location(2) iExtra_0 : vec4<f32>,
    @location(3) iColor_0 : vec4<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0, @builtin(vertex_index) vertexId_0 : u32) -> VertexOut_0
{
    const _S2 : vec2<f32> = vec2<f32>(1.0f, 0.0f);
    const _S3 : vec2<f32> = vec2<f32>(0.0f, 1.0f);
    var corners_0 : array<vec2<f32>, i32(6)> = array<vec2<f32>, i32(6)>( vec2<f32>(0.0f, 0.0f), _S2, _S3, _S3, _S2, vec2<f32>(1.0f, 1.0f) );
    var _S4 : vec2<f32> = vec2<f32>(1.0f);
    var px_0 : vec2<f32> = mix(_S1.iBounds_0.xy - _S4, _S1.iBounds_0.zw + _S4, corners_0[vertexId_0]);
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(px_0.x / shapePass_0.uResolution_0.x * 2.0f - 1.0f, 1.0f - px_0.y / shapePass_0.uResolution_0.y * 2.0f, 0.0f, 1.0f);
    o_0.bounds_0 = _S1.iBounds_0;
    o_0.shape_0 = _S1.iShape_0;
    o_0.extra_0 = _S1.iExtra_0;
    o_0.color_0 = _S1.iColor_0;
    return o_0;
}

