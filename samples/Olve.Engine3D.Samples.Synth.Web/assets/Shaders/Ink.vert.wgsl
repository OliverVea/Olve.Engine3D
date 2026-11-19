struct _MatrixStorage_float4x4std140_0
{
    @align(16) data_0 : array<vec4<f32>, i32(4)>,
};

struct SpacePassData_std140_0
{
    @align(16) view_0 : _MatrixStorage_float4x4std140_0,
    @align(16) projection_0 : _MatrixStorage_float4x4std140_0,
    @align(16) cameraPosition_0 : vec3<f32>,
    @align(16) skyAmbient_0 : vec3<f32>,
    @align(16) groundAmbient_0 : vec3<f32>,
    @align(16) keyPosition_0 : vec3<f32>,
    @align(16) keyColor_0 : vec3<f32>,
    @align(16) light0Position_0 : vec3<f32>,
    @align(16) light0Color_0 : vec3<f32>,
    @align(16) light1Position_0 : vec3<f32>,
    @align(16) light1Color_0 : vec3<f32>,
    @align(16) glowOnly_0 : vec4<f32>,
};

@binding(0) @group(0) var<uniform> spacePass_0 : SpacePassData_std140_0;
struct Matrix4Rows_0
{
     _S1 : vec4<f32>,
     _S2 : vec4<f32>,
     _S3 : vec4<f32>,
     _S4 : vec4<f32>,
};

fn Matrix4Rows_toMatrix_0( this_0 : Matrix4Rows_0) -> mat4x4<f32>
{
    return mat4x4<f32>(this_0._S1, this_0._S2, this_0._S3, this_0._S4);
}

struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) design_0 : vec2<f32>,
    @location(1) worldPosition_0 : vec3<f32>,
    @location(2) normal_0 : vec3<f32>,
    @location(3) right_0 : vec3<f32>,
    @location(4) down_0 : vec3<f32>,
    @location(5) bounds_0 : vec4<f32>,
    @location(6) shape_0 : vec4<f32>,
    @location(7) extra_0 : vec4<f32>,
    @location(8) color_0 : vec4<f32>,
    @location(9) glow_0 : f32,
};

struct vertexInput_0
{
    @location(0) row0_0 : vec4<f32>,
    @location(1) row1_0 : vec4<f32>,
    @location(2) row2_0 : vec4<f32>,
    @location(3) row3_0 : vec4<f32>,
    @location(4) iBounds_0 : vec4<f32>,
    @location(5) iShape_0 : vec4<f32>,
    @location(6) iExtra_0 : vec4<f32>,
    @location(7) iColor_0 : vec4<f32>,
    @location(8) iGlow_0 : vec4<f32>,
};

struct vertexInput_1
{
     iSurface_0 : Matrix4Rows_0,
     _S5 : vec4<f32>,
     _S6 : vec4<f32>,
     _S7 : vec4<f32>,
     _S8 : vec4<f32>,
     _S9 : vec4<f32>,
};

@vertex
fn vertexMain( _S10 : vertexInput_0, @builtin(vertex_index) vertexId_0 : u32) -> VertexOut_0
{
    var _S11 : vertexInput_1;
    _S11.iSurface_0._S1 = _S10.row0_0;
    _S11.iSurface_0._S2 = _S10.row1_0;
    _S11.iSurface_0._S3 = _S10.row2_0;
    _S11.iSurface_0._S4 = _S10.row3_0;
    _S11._S5 = _S10.iBounds_0;
    _S11._S6 = _S10.iShape_0;
    _S11._S7 = _S10.iExtra_0;
    _S11._S8 = _S10.iColor_0;
    _S11._S9 = _S10.iGlow_0;
    const _S12 : vec2<f32> = vec2<f32>(1.0f, 0.0f);
    const _S13 : vec2<f32> = vec2<f32>(0.0f, 1.0f);
    var corners_0 : array<vec2<f32>, i32(6)> = array<vec2<f32>, i32(6)>( vec2<f32>(0.0f, 0.0f), _S12, _S13, _S13, _S12, vec2<f32>(1.0f, 1.0f) );
    var _S14 : vec2<f32> = vec2<f32>(1.0f);
    var design_1 : vec2<f32> = mix(_S11._S5.xy - _S14, _S11._S5.zw + _S14, corners_0[vertexId_0]);
    var surface_0 : mat4x4<f32> = Matrix4Rows_toMatrix_0(_S11.iSurface_0);
    var worldPosition_1 : vec4<f32> = (((surface_0) * (vec4<f32>(design_1, 0.0f, 1.0f))));
    var o_0 : VertexOut_0;
    o_0.position_0 = (((mat4x4<f32>(spacePass_0.projection_0.data_0[i32(0)][i32(0)], spacePass_0.projection_0.data_0[i32(0)][i32(1)], spacePass_0.projection_0.data_0[i32(0)][i32(2)], spacePass_0.projection_0.data_0[i32(0)][i32(3)], spacePass_0.projection_0.data_0[i32(1)][i32(0)], spacePass_0.projection_0.data_0[i32(1)][i32(1)], spacePass_0.projection_0.data_0[i32(1)][i32(2)], spacePass_0.projection_0.data_0[i32(1)][i32(3)], spacePass_0.projection_0.data_0[i32(2)][i32(0)], spacePass_0.projection_0.data_0[i32(2)][i32(1)], spacePass_0.projection_0.data_0[i32(2)][i32(2)], spacePass_0.projection_0.data_0[i32(2)][i32(3)], spacePass_0.projection_0.data_0[i32(3)][i32(0)], spacePass_0.projection_0.data_0[i32(3)][i32(1)], spacePass_0.projection_0.data_0[i32(3)][i32(2)], spacePass_0.projection_0.data_0[i32(3)][i32(3)])) * ((((mat4x4<f32>(spacePass_0.view_0.data_0[i32(0)][i32(0)], spacePass_0.view_0.data_0[i32(0)][i32(1)], spacePass_0.view_0.data_0[i32(0)][i32(2)], spacePass_0.view_0.data_0[i32(0)][i32(3)], spacePass_0.view_0.data_0[i32(1)][i32(0)], spacePass_0.view_0.data_0[i32(1)][i32(1)], spacePass_0.view_0.data_0[i32(1)][i32(2)], spacePass_0.view_0.data_0[i32(1)][i32(3)], spacePass_0.view_0.data_0[i32(2)][i32(0)], spacePass_0.view_0.data_0[i32(2)][i32(1)], spacePass_0.view_0.data_0[i32(2)][i32(2)], spacePass_0.view_0.data_0[i32(2)][i32(3)], spacePass_0.view_0.data_0[i32(3)][i32(0)], spacePass_0.view_0.data_0[i32(3)][i32(1)], spacePass_0.view_0.data_0[i32(3)][i32(2)], spacePass_0.view_0.data_0[i32(3)][i32(3)])) * (worldPosition_1))))));
    o_0.design_0 = design_1;
    o_0.worldPosition_0 = worldPosition_1.xyz;
    o_0.normal_0 = surface_0[i32(2)].xyz;
    o_0.right_0 = normalize(surface_0[i32(0)].xyz);
    o_0.down_0 = normalize(surface_0[i32(1)].xyz);
    o_0.bounds_0 = _S11._S5;
    o_0.shape_0 = _S11._S6;
    o_0.extra_0 = _S11._S7;
    o_0.color_0 = _S11._S8;
    o_0.glow_0 = _S11._S9.x;
    return o_0;
}

