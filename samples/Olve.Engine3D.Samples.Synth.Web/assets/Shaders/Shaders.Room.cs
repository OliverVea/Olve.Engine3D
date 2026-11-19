// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
global using RoomShaderId = Olve.Engine3D.Rendering.Shaders.ShaderId<Olve.Generated.Shaders.Shaders.Shared.RoomVertex, Olve.Generated.Shaders.Shaders.Room.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Generated.Shaders.Shaders.Shared.SpacePass, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>;
global using RoomGroupId = Olve.Engine3D.Rendering.Instancing.GroupId<Olve.Generated.Shaders.Shaders.Shared.RoomVertex, Olve.Generated.Shaders.Shaders.Room.Instance>;
// ReSharper disable RedundantUsingDirective
using Olve.Engine3D;
using Olve.Engine3D.Geometry;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Utilities.Ids;
using System.Numerics;
using Olve.Engine3D.Math;

// ReSharper disable once CheckNamespace
namespace Olve.Generated.Shaders;


public static partial class Shaders
{

    public partial class Room : IShaderDefinition<Shared.RoomVertex, Room.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Generated.Shaders.Shaders.Shared.SpacePass, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>
    {
        /// <summary>
        /// This shader's definition, registered with <c>ShaderManager.Register</c>. Definitions are immutable, so each shader has exactly one.
        /// </summary>
        public static Room Definition { get; } = new();

        private Room()
        {
        }


        public readonly record struct Instance(
            Matrix4x4 iWorld
        ) : IInstanceData
        {
            public static int FloatCount => 16;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = iWorld.M11; buffer[i++] = iWorld.M12; buffer[i++] = iWorld.M13; buffer[i++] = iWorld.M14; buffer[i++] = iWorld.M21; buffer[i++] = iWorld.M22; buffer[i++] = iWorld.M23; buffer[i++] = iWorld.M24; buffer[i++] = iWorld.M31; buffer[i++] = iWorld.M32; buffer[i++] = iWorld.M33; buffer[i++] = iWorld.M34; buffer[i++] = iWorld.M41; buffer[i++] = iWorld.M42; buffer[i++] = iWorld.M43; buffer[i++] = iWorld.M44;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 64,
                Instanced: true,
                Attributes:
                [
                    new(Location: 4, ComponentCount: 4, ByteOffset: 0), // iWorld[0]
                    new(Location: 5, ComponentCount: 4, ByteOffset: 16), // iWorld[1]
                    new(Location: 6, ComponentCount: 4, ByteOffset: 32), // iWorld[2]
                    new(Location: 7, ComponentCount: 4, ByteOffset: 48), // iWorld[3]
                ]);
        }

        #region ShaderSource

        public ShaderData ShaderData { get; } = new()
        {
            Name = "Room",
            Wgsl = new WgslShaderData
            {
                VertexSource = @"struct _MatrixStorage_float4x4std140_0
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
    @location(0) worldPosition_0 : vec3<f32>,
    @location(1) normal_0 : vec3<f32>,
    @location(2) albedo_0 : vec4<f32>,
    @location(3) finish_0 : vec4<f32>,
};

struct vertexInput_0
{
    @location(0) position_1 : vec3<f32>,
    @location(1) normal_1 : vec3<f32>,
    @location(2) albedo_1 : vec4<f32>,
    @location(3) finish_1 : vec4<f32>,
    @location(4) row0_0 : vec4<f32>,
    @location(5) row1_0 : vec4<f32>,
    @location(6) row2_0 : vec4<f32>,
    @location(7) row3_0 : vec4<f32>,
};

struct vertexInput_1
{
     _S5 : vec3<f32>,
     _S6 : vec3<f32>,
     _S7 : vec4<f32>,
     _S8 : vec4<f32>,
     iWorld_0 : Matrix4Rows_0,
};

@vertex
fn vertexMain( _S9 : vertexInput_0) -> VertexOut_0
{
    var _S10 : vertexInput_1;
    _S10._S5 = _S9.position_1;
    _S10._S6 = _S9.normal_1;
    _S10._S7 = _S9.albedo_1;
    _S10._S8 = _S9.finish_1;
    _S10.iWorld_0._S1 = _S9.row0_0;
    _S10.iWorld_0._S2 = _S9.row1_0;
    _S10.iWorld_0._S3 = _S9.row2_0;
    _S10.iWorld_0._S4 = _S9.row3_0;
    var world_0 : mat4x4<f32> = Matrix4Rows_toMatrix_0(_S10.iWorld_0);
    var worldPosition_1 : vec4<f32> = (((world_0) * (vec4<f32>(_S10._S5, 1.0f))));
    var o_0 : VertexOut_0;
    o_0.worldPosition_0 = worldPosition_1.xyz;
    o_0.normal_0 = (((mat3x3<f32>(world_0[i32(0)].xyz, world_0[i32(1)].xyz, world_0[i32(2)].xyz)) * (_S10._S6)));
    o_0.albedo_0 = _S10._S7;
    o_0.finish_0 = _S10._S8;
    o_0.position_0 = (((mat4x4<f32>(spacePass_0.projection_0.data_0[i32(0)][i32(0)], spacePass_0.projection_0.data_0[i32(0)][i32(1)], spacePass_0.projection_0.data_0[i32(0)][i32(2)], spacePass_0.projection_0.data_0[i32(0)][i32(3)], spacePass_0.projection_0.data_0[i32(1)][i32(0)], spacePass_0.projection_0.data_0[i32(1)][i32(1)], spacePass_0.projection_0.data_0[i32(1)][i32(2)], spacePass_0.projection_0.data_0[i32(1)][i32(3)], spacePass_0.projection_0.data_0[i32(2)][i32(0)], spacePass_0.projection_0.data_0[i32(2)][i32(1)], spacePass_0.projection_0.data_0[i32(2)][i32(2)], spacePass_0.projection_0.data_0[i32(2)][i32(3)], spacePass_0.projection_0.data_0[i32(3)][i32(0)], spacePass_0.projection_0.data_0[i32(3)][i32(1)], spacePass_0.projection_0.data_0[i32(3)][i32(2)], spacePass_0.projection_0.data_0[i32(3)][i32(3)])) * ((((mat4x4<f32>(spacePass_0.view_0.data_0[i32(0)][i32(0)], spacePass_0.view_0.data_0[i32(0)][i32(1)], spacePass_0.view_0.data_0[i32(0)][i32(2)], spacePass_0.view_0.data_0[i32(0)][i32(3)], spacePass_0.view_0.data_0[i32(1)][i32(0)], spacePass_0.view_0.data_0[i32(1)][i32(1)], spacePass_0.view_0.data_0[i32(1)][i32(2)], spacePass_0.view_0.data_0[i32(1)][i32(3)], spacePass_0.view_0.data_0[i32(2)][i32(0)], spacePass_0.view_0.data_0[i32(2)][i32(1)], spacePass_0.view_0.data_0[i32(2)][i32(2)], spacePass_0.view_0.data_0[i32(2)][i32(3)], spacePass_0.view_0.data_0[i32(3)][i32(0)], spacePass_0.view_0.data_0[i32(3)][i32(1)], spacePass_0.view_0.data_0[i32(3)][i32(2)], spacePass_0.view_0.data_0[i32(3)][i32(3)])) * (worldPosition_1))))));
    return o_0;
}

",
                FragmentSource = @"struct _MatrixStorage_float4x4std140_0
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
fn toLinear_0( c_0 : vec3<f32>) -> vec3<f32>
{
    return pow(max(c_0, vec3<f32>(0.0f)), vec3<f32>(2.2000000476837158f));
}

fn hash_0( p_0 : vec3<f32>) -> f32
{
    var _S1 : vec3<f32> = fract(p_0 * vec3<f32>(0.31830990314483643f) + vec3<f32>(0.10000000149011612f)) * vec3<f32>(17.0f);
    var _S2 : f32 = _S1.x;
    var _S3 : f32 = _S1.y;
    var _S4 : f32 = _S1.z;
    return fract(_S2 * _S3 * _S4 * (_S2 + _S3 + _S4));
}

fn noise_0( x_0 : vec3<f32>) -> f32
{
    var i_0 : vec3<f32> = floor(x_0);
    var f_0 : vec3<f32> = fract(x_0);
    var f_1 : vec3<f32> = f_0 * f_0 * (vec3<f32>(3.0f) - vec3<f32>(2.0f) * f_0);
    var _S5 : f32 = f_1.x;
    var _S6 : f32 = f_1.y;
    return mix(mix(mix(hash_0(i_0), hash_0(i_0 + vec3<f32>(1.0f, 0.0f, 0.0f)), _S5), mix(hash_0(i_0 + vec3<f32>(0.0f, 1.0f, 0.0f)), hash_0(i_0 + vec3<f32>(1.0f, 1.0f, 0.0f)), _S5), _S6), mix(mix(hash_0(i_0 + vec3<f32>(0.0f, 0.0f, 1.0f)), hash_0(i_0 + vec3<f32>(1.0f, 0.0f, 1.0f)), _S5), mix(hash_0(i_0 + vec3<f32>(0.0f, 1.0f, 1.0f)), hash_0(i_0 + vec3<f32>(1.0f, 1.0f, 1.0f)), _S5), _S6), f_1.z);
}

fn woodGrain_0( p_1 : vec3<f32>,  albedo_0 : vec3<f32>) -> vec3<f32>
{
    var _S7 : f32 = p_1.z;
    var rings_0 : f32 = fract(_S7 * 38.0f + noise_0(vec3<f32>(p_1.x * 2.5f, p_1.y * 30.0f, _S7 * 30.0f)) * 2.5999999046325684f + noise_0(p_1 * vec3<f32>(0.89999997615814209f, 6.0f, 6.0f)) * 1.2000000476837158f);
    return albedo_0 * vec3<f32>((0.72000002861022949f + 0.40000000596046448f * (smoothstep(0.0f, 0.30000001192092896f, rings_0) * smoothstep(1.0f, 0.60000002384185791f, rings_0))));
}

fn whiteLight_0( lightPosition_0 : vec3<f32>,  position_0 : vec3<f32>,  normal_0 : vec3<f32>,  toEye_0 : vec3<f32>,  albedo_1 : vec3<f32>,  shine_0 : f32) -> vec3<f32>
{
    var toLight_0 : vec3<f32> = lightPosition_0 - position_0;
    var distance2_0 : f32 = dot(toLight_0, toLight_0);
    var l_0 : vec3<f32> = toLight_0 * vec3<f32>(inverseSqrt(max(distance2_0, 9.99999997475242708e-07f)));
    var _S8 : f32 = dot(normal_0, l_0);
    var exponent_0 : f32 = mix(12.0f, 80.0f, shine_0);
    return (albedo_1 * vec3<f32>(saturate((_S8 + 0.20000000298023224f) / 1.2000000476837158f)) + vec3<f32>((pow(max(dot(normal_0, normalize(l_0 + toEye_0)), 0.0f), exponent_0) * shine_0 * (exponent_0 + 8.0f) / 160.0f * step(0.0f, _S8)))) * vec3<f32>((1.0f / (1.0f + 0.80000001192092896f * distance2_0)));
}

fn rollOff_0( c_1 : vec3<f32>) -> vec3<f32>
{
    var knee_0 : vec3<f32> = vec3<f32>(0.75f);
    var _S9 : vec3<f32> = max(c_1 - knee_0, vec3<f32>(0.0f));
    return min(c_1, knee_0) + _S9 / (vec3<f32>(1.0f) + _S9 * vec3<f32>(2.0f));
}

fn toSrgb_0( c_2 : vec3<f32>) -> vec3<f32>
{
    return pow(max(c_2, vec3<f32>(0.0f)), vec3<f32>(0.45454543828964233f));
}

struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) worldPosition_0 : vec3<f32>,
    @location(1) normal_1 : vec3<f32>,
    @location(2) albedo_2 : vec4<f32>,
    @location(3) finish_0 : vec4<f32>,
};

fn shade_0( _S10 : vec3<f32>,  _S11 : vec3<f32>,  _S12 : vec3<f32>,  _S13 : f32,  _S14 : vec3<f32>) -> vec3<f32>
{
    var toEye_1 : vec3<f32> = normalize(spacePass_0.cameraPosition_0 - _S10);
    return _S12 * mix(spacePass_0.groundAmbient_0, spacePass_0.skyAmbient_0, vec3<f32>((_S11.y * 0.5f + 0.5f))) + whiteLight_0(spacePass_0.keyPosition_0, _S10, _S11, toEye_1, _S12, _S13) * spacePass_0.keyColor_0 + whiteLight_0(spacePass_0.light0Position_0, _S10, _S11, toEye_1, _S12, _S13) * spacePass_0.light0Color_0 + whiteLight_0(spacePass_0.light1Position_0, _S10, _S11, toEye_1, _S12, _S13) * spacePass_0.light1Color_0 + _S14;
}

fn isGlowPass_0() -> bool
{
    return ((spacePass_0.glowOnly_0.x) > 0.5f);
}

@fragment
fn fragmentMain( _S15 : pixelInput_0, @builtin(position) position_1 : vec4<f32>) -> FragmentOut_0
{
    var normal_2 : vec3<f32> = normalize(_S15.normal_1);
    var albedo_3 : vec3<f32> = toLinear_0(_S15.albedo_2.xyz);
    var _S16 : f32 = _S15.finish_0.x;
    var albedo_4 : vec3<f32>;
    if((_S16 > 0.0f))
    {
        albedo_4 = mix(albedo_3, woodGrain_0(_S15.worldPosition_0, albedo_3), vec3<f32>(_S16));
    }
    else
    {
        albedo_4 = albedo_3;
    }
    var _S17 : vec3<f32> = shade_0(_S15.worldPosition_0, normal_2, albedo_4, _S15.albedo_2.w, vec3<f32>(0.0f));
    var o_0 : FragmentOut_0;
    var _S18 : vec4<f32>;
    if(isGlowPass_0())
    {
        _S18 = vec4<f32>(0.0f, 0.0f, 0.0f, 1.0f);
    }
    else
    {
        _S18 = vec4<f32>(toSrgb_0(rollOff_0(_S17)), 1.0f);
    }
    o_0.fragColor_0 = _S18;
    return o_0;
}

",
                Pass = new WgslInputsBindings(0, []),
                Group = new WgslInputsBindings(null, []),
            },
        };

        #endregion
    }
}
