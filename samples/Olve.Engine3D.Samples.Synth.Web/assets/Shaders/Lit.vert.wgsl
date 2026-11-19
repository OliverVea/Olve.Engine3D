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

fn inverse3x3_0( m_0 : mat3x3<f32>) -> mat3x3<f32>
{
    var r0_0 : vec3<f32> = cross(m_0[i32(1)], m_0[i32(2)]);
    return transpose(mat3x3<f32>(r0_0, cross(m_0[i32(2)], m_0[i32(0)]), cross(m_0[i32(0)], m_0[i32(1)]))) * (1.0f / dot(m_0[i32(0)], r0_0));
}

struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) worldPosition_0 : vec3<f32>,
    @location(1) localPosition_0 : vec3<f32>,
    @location(2) normal_0 : vec3<f32>,
    @location(3) tangent_0 : vec3<f32>,
    @location(4) albedo_0 : vec4<f32>,
    @location(5) emissive_0 : vec4<f32>,
    @location(6) finish_0 : vec4<f32>,
};

struct vertexInput_0
{
    @location(0) position_1 : vec3<f32>,
    @location(1) normal_1 : vec3<f32>,
    @location(2) row0_0 : vec4<f32>,
    @location(3) row1_0 : vec4<f32>,
    @location(4) row2_0 : vec4<f32>,
    @location(5) row3_0 : vec4<f32>,
    @location(6) iAlbedo_0 : vec4<f32>,
    @location(7) iEmissive_0 : vec4<f32>,
    @location(8) iFinish_0 : vec4<f32>,
};

struct vertexInput_1
{
     _S5 : vec3<f32>,
     _S6 : vec3<f32>,
     iWorld_0 : Matrix4Rows_0,
     _S7 : vec4<f32>,
     _S8 : vec4<f32>,
     _S9 : vec4<f32>,
};

@vertex
fn vertexMain( _S10 : vertexInput_0) -> VertexOut_0
{
    var _S11 : vertexInput_1;
    _S11._S5 = _S10.position_1;
    _S11._S6 = _S10.normal_1;
    _S11.iWorld_0._S1 = _S10.row0_0;
    _S11.iWorld_0._S2 = _S10.row1_0;
    _S11.iWorld_0._S3 = _S10.row2_0;
    _S11.iWorld_0._S4 = _S10.row3_0;
    _S11._S7 = _S10.iAlbedo_0;
    _S11._S8 = _S10.iEmissive_0;
    _S11._S9 = _S10.iFinish_0;
    var _S12 : vertexInput_1 = _S11;
    var world_0 : mat4x4<f32> = Matrix4Rows_toMatrix_0(_S11.iWorld_0);
    var worldPosition_1 : vec4<f32> = (((world_0) * (vec4<f32>(_S11._S5, 1.0f))));
    var o_0 : VertexOut_0;
    o_0.worldPosition_0 = worldPosition_1.xyz;
    o_0.localPosition_0 = _S11._S5;
    var _S13 : vec3<f32> = world_0[i32(0)].xyz;
    var _S14 : mat3x3<f32> = mat3x3<f32>(_S13, world_0[i32(1)].xyz, world_0[i32(2)].xyz);
    o_0.normal_0 = (((_S11._S6) * (inverse3x3_0(_S14))));
    var _S15 : vec3<f32>;
    if(((_S11._S9.w) > 0.5f))
    {
        _S15 = (((_S14) * (vec3<f32>(- _S12._S5.z, 0.0f, _S12._S5.x))));
    }
    else
    {
        _S15 = _S13;
    }
    o_0.tangent_0 = _S15;
    o_0.albedo_0 = _S12._S7;
    o_0.emissive_0 = _S12._S8;
    o_0.finish_0 = _S12._S9;
    o_0.position_0 = (((mat4x4<f32>(spacePass_0.projection_0.data_0[i32(0)][i32(0)], spacePass_0.projection_0.data_0[i32(0)][i32(1)], spacePass_0.projection_0.data_0[i32(0)][i32(2)], spacePass_0.projection_0.data_0[i32(0)][i32(3)], spacePass_0.projection_0.data_0[i32(1)][i32(0)], spacePass_0.projection_0.data_0[i32(1)][i32(1)], spacePass_0.projection_0.data_0[i32(1)][i32(2)], spacePass_0.projection_0.data_0[i32(1)][i32(3)], spacePass_0.projection_0.data_0[i32(2)][i32(0)], spacePass_0.projection_0.data_0[i32(2)][i32(1)], spacePass_0.projection_0.data_0[i32(2)][i32(2)], spacePass_0.projection_0.data_0[i32(2)][i32(3)], spacePass_0.projection_0.data_0[i32(3)][i32(0)], spacePass_0.projection_0.data_0[i32(3)][i32(1)], spacePass_0.projection_0.data_0[i32(3)][i32(2)], spacePass_0.projection_0.data_0[i32(3)][i32(3)])) * ((((mat4x4<f32>(spacePass_0.view_0.data_0[i32(0)][i32(0)], spacePass_0.view_0.data_0[i32(0)][i32(1)], spacePass_0.view_0.data_0[i32(0)][i32(2)], spacePass_0.view_0.data_0[i32(0)][i32(3)], spacePass_0.view_0.data_0[i32(1)][i32(0)], spacePass_0.view_0.data_0[i32(1)][i32(1)], spacePass_0.view_0.data_0[i32(1)][i32(2)], spacePass_0.view_0.data_0[i32(1)][i32(3)], spacePass_0.view_0.data_0[i32(2)][i32(0)], spacePass_0.view_0.data_0[i32(2)][i32(1)], spacePass_0.view_0.data_0[i32(2)][i32(2)], spacePass_0.view_0.data_0[i32(2)][i32(3)], spacePass_0.view_0.data_0[i32(3)][i32(0)], spacePass_0.view_0.data_0[i32(3)][i32(1)], spacePass_0.view_0.data_0[i32(3)][i32(2)], spacePass_0.view_0.data_0[i32(3)][i32(3)])) * (worldPosition_1))))));
    return o_0;
}

