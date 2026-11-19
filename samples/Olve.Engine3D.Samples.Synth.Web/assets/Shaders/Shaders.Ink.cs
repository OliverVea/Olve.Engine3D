// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
global using InkShaderId = Olve.Engine3D.Rendering.Shaders.ShaderId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Ink.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Generated.Shaders.Shaders.Shared.SpacePass, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>;
global using InkGroupId = Olve.Engine3D.Rendering.Instancing.GroupId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Ink.Instance>;
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

    public partial class Ink : IShaderDefinition<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Ink.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Generated.Shaders.Shaders.Shared.SpacePass, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>
    {
        /// <summary>
        /// This shader's definition, registered with <c>ShaderManager.Register</c>. Definitions are immutable, so each shader has exactly one.
        /// </summary>
        public static Ink Definition { get; } = new();

        private Ink()
        {
        }


        public readonly record struct Instance(
            Matrix4x4 iSurface,
            Vector4 iBounds,
            Vector4 iShape,
            Vector4 iExtra,
            Vector4 iColor,
            Vector4 iGlow
        ) : IInstanceData
        {
            public static int FloatCount => 36;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = iSurface.M11; buffer[i++] = iSurface.M12; buffer[i++] = iSurface.M13; buffer[i++] = iSurface.M14; buffer[i++] = iSurface.M21; buffer[i++] = iSurface.M22; buffer[i++] = iSurface.M23; buffer[i++] = iSurface.M24; buffer[i++] = iSurface.M31; buffer[i++] = iSurface.M32; buffer[i++] = iSurface.M33; buffer[i++] = iSurface.M34; buffer[i++] = iSurface.M41; buffer[i++] = iSurface.M42; buffer[i++] = iSurface.M43; buffer[i++] = iSurface.M44;
                buffer[i++] = iBounds.X; buffer[i++] = iBounds.Y; buffer[i++] = iBounds.Z; buffer[i++] = iBounds.W;
                buffer[i++] = iShape.X; buffer[i++] = iShape.Y; buffer[i++] = iShape.Z; buffer[i++] = iShape.W;
                buffer[i++] = iExtra.X; buffer[i++] = iExtra.Y; buffer[i++] = iExtra.Z; buffer[i++] = iExtra.W;
                buffer[i++] = iColor.X; buffer[i++] = iColor.Y; buffer[i++] = iColor.Z; buffer[i++] = iColor.W;
                buffer[i++] = iGlow.X; buffer[i++] = iGlow.Y; buffer[i++] = iGlow.Z; buffer[i++] = iGlow.W;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 144,
                Instanced: true,
                Attributes:
                [
                    new(Location: 0, ComponentCount: 4, ByteOffset: 0), // iSurface[0]
                    new(Location: 1, ComponentCount: 4, ByteOffset: 16), // iSurface[1]
                    new(Location: 2, ComponentCount: 4, ByteOffset: 32), // iSurface[2]
                    new(Location: 3, ComponentCount: 4, ByteOffset: 48), // iSurface[3]
                    new(Location: 4, ComponentCount: 4, ByteOffset: 64), // iBounds
                    new(Location: 5, ComponentCount: 4, ByteOffset: 80), // iShape
                    new(Location: 6, ComponentCount: 4, ByteOffset: 96), // iExtra
                    new(Location: 7, ComponentCount: 4, ByteOffset: 112), // iColor
                    new(Location: 8, ComponentCount: 4, ByteOffset: 128), // iGlow
                ]);
        }

        #region ShaderSource

        public ShaderData ShaderData { get; } = new()
        {
            Name = "Ink",
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
fn roundedBox_0( p_0 : vec2<f32>,  halfSize_0 : vec2<f32>,  radius_0 : f32) -> f32
{
    var q_0 : vec2<f32> = abs(p_0) - halfSize_0 + vec2<f32>(radius_0);
    return min(max(q_0.x, q_0.y), 0.0f) + length(max(q_0, vec2<f32>(0.0f))) - radius_0;
}

fn segment_0( p_1 : vec2<f32>,  a_0 : vec2<f32>,  b_0 : vec2<f32>,  halfWidth_0 : f32) -> f32
{
    var pa_0 : vec2<f32> = p_1 - a_0;
    var ba_0 : vec2<f32> = b_0 - a_0;
    return length(pa_0 - ba_0 * vec2<f32>(clamp(dot(pa_0, ba_0) / max(dot(ba_0, ba_0), 9.9999997473787516e-05f), 0.0f, 1.0f))) - halfWidth_0;
}

fn glyphDot_0( low_0 : u32,  high_0 : u32,  x_0 : u32,  y_0 : u32) -> bool
{
    var bit_0 : u32;
    if((y_0 < u32(4)))
    {
        bit_0 = (low_0 >> ((y_0 * u32(5) + x_0)));
    }
    else
    {
        bit_0 = (high_0 >> (((y_0 - u32(4)) * u32(5) + x_0)));
    }
    return ((bit_0 & (u32(1)))) != u32(0);
}

fn toLinear_0( c_0 : vec3<f32>) -> vec3<f32>
{
    return pow(max(c_0, vec3<f32>(0.0f)), vec3<f32>(2.2000000476837158f));
}

fn whiteLight_0( lightPosition_0 : vec3<f32>,  position_0 : vec3<f32>,  normal_0 : vec3<f32>,  toEye_0 : vec3<f32>,  albedo_0 : vec3<f32>,  shine_0 : f32) -> vec3<f32>
{
    var toLight_0 : vec3<f32> = lightPosition_0 - position_0;
    var distance2_0 : f32 = dot(toLight_0, toLight_0);
    var l_0 : vec3<f32> = toLight_0 * vec3<f32>(inverseSqrt(max(distance2_0, 9.99999997475242708e-07f)));
    var _S1 : f32 = dot(normal_0, l_0);
    var exponent_0 : f32 = mix(12.0f, 80.0f, shine_0);
    return (albedo_0 * vec3<f32>(saturate((_S1 + 0.20000000298023224f) / 1.2000000476837158f)) + vec3<f32>((pow(max(dot(normal_0, normalize(l_0 + toEye_0)), 0.0f), exponent_0) * shine_0 * (exponent_0 + 8.0f) / 160.0f * step(0.0f, _S1)))) * vec3<f32>((1.0f / (1.0f + 0.80000001192092896f * distance2_0)));
}

fn rollOff_0( c_1 : vec3<f32>) -> vec3<f32>
{
    var knee_0 : vec3<f32> = vec3<f32>(0.75f);
    var _S2 : vec3<f32> = max(c_1 - knee_0, vec3<f32>(0.0f));
    return min(c_1, knee_0) + _S2 / (vec3<f32>(1.0f) + _S2 * vec3<f32>(2.0f));
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
    @location(0) design_0 : vec2<f32>,
    @location(1) worldPosition_0 : vec3<f32>,
    @location(2) normal_1 : vec3<f32>,
    @location(3) right_0 : vec3<f32>,
    @location(4) down_0 : vec3<f32>,
    @location(5) bounds_0 : vec4<f32>,
    @location(6) shape_0 : vec4<f32>,
    @location(7) extra_0 : vec4<f32>,
    @location(8) color_0 : vec4<f32>,
    @location(9) glow_0 : f32,
};

fn isGlowPass_0() -> bool
{
    return ((spacePass_0.glowOnly_0.x) > 0.5f);
}

fn shade_0( _S3 : vec3<f32>,  _S4 : vec3<f32>,  _S5 : vec3<f32>,  _S6 : f32,  _S7 : vec3<f32>) -> vec3<f32>
{
    var toEye_1 : vec3<f32> = normalize(spacePass_0.cameraPosition_0 - _S3);
    return _S5 * mix(spacePass_0.groundAmbient_0, spacePass_0.skyAmbient_0, vec3<f32>((_S4.y * 0.5f + 0.5f))) + whiteLight_0(spacePass_0.keyPosition_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.keyColor_0 + whiteLight_0(spacePass_0.light0Position_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.light0Color_0 + whiteLight_0(spacePass_0.light1Position_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.light1Color_0 + _S7;
}

@fragment
fn fragmentMain( _S8 : pixelInput_0, @builtin(position) position_1 : vec4<f32>) -> FragmentOut_0
{
    var _S9 : vec2<f32> = _S8.bounds_0.xy;
    var _S10 : vec2<f32> = _S8.bounds_0.zw;
    var _S11 : vec2<f32> = vec2<f32>(0.5f);
    var _S12 : vec2<f32> = _S10 - _S9;
    var halfSize_1 : vec2<f32> = _S12 * _S11;
    var p_2 : vec2<f32> = _S8.design_0 - (_S9 + _S10) * _S11;
    var kind_0 : i32 = i32(_S8.shape_0.x + 0.5f);
    var _S13 : f32 = max(length((fwidth((_S8.design_0)))) * 0.70709997415542603f, 9.9999997473787516e-05f);
    var normal_2 : vec3<f32> = normalize(_S8.normal_1);
    var d_0 : f32;
    var shadow_0 : f32;
    var _S14 : bool;
    var normal_3 : vec3<f32>;
    if(kind_0 == i32(0))
    {
        var d_1 : f32 = roundedBox_0(p_2, halfSize_1, _S8.shape_0.y);
        var _S15 : f32 = _S8.shape_0.z;
        if((_S15 > 0.0f))
        {
            var _S16 : f32 = _S15 * 0.5f;
            d_0 = abs(d_1 + _S16) - _S16;
        }
        else
        {
            d_0 = d_1;
        }
        shadow_0 = 0.0f;
        normal_3 = normal_2;
    }
    else
    {
        if(kind_0 == i32(1))
        {
            var r_0 : f32 = length(p_2);
            var _S17 : f32 = halfSize_1.x;
            var d_2 : f32 = r_0 - _S17;
            var _S18 : f32 = _S8.shape_0.y;
            if((_S18 > 0.0f))
            {
                d_0 = max(d_2, _S18 - r_0);
            }
            else
            {
                d_0 = d_2;
            }
            var _S19 : f32 = _S8.extra_0.z;
            if((_S19 > 0.0f))
            {
                var q_1 : vec2<f32> = p_2 / vec2<f32>(_S17);
                normal_3 = normalize(normal_2 * vec3<f32>(sqrt(max(1.0f - dot(q_1, q_1), 0.05000000074505806f))) + (_S8.right_0 * vec3<f32>(q_1.x) + _S8.down_0 * vec3<f32>(q_1.y)) * vec3<f32>(_S19));
            }
            else
            {
                normal_3 = normal_2;
            }
            shadow_0 = 0.0f;
        }
        else
        {
            if(kind_0 == i32(2))
            {
                var r_1 : f32 = length(p_2);
                var _S20 : f32 = _S8.extra_0.x;
                var _S21 : f32 = _S8.extra_0.y;
                d_0 = max(max(r_1 - halfSize_1.x, _S8.shape_0.y - r_1), (abs(atan2(p_2.x, - p_2.y) - (_S20 + _S21) * 0.5f) - (_S21 - _S20) * 0.5f) * r_1);
                shadow_0 = 0.0f;
            }
            else
            {
                if(kind_0 == i32(3))
                {
                    var _S22 : vec2<f32> = _S8.extra_0.xy;
                    var _S23 : vec2<f32> = _S8.extra_0.zw;
                    var d_3 : f32 = segment_0(_S8.design_0, _S22, _S23, _S8.shape_0.y * 0.5f);
                    var _S24 : f32 = _S8.shape_0.z;
                    if((_S24 > 0.0f))
                    {
                        var along_0 : vec2<f32> = _S23 - _S22;
                        d_0 = max(d_3, (abs(fract(dot(_S8.design_0 - _S22, along_0) / max(length(along_0), 9.9999997473787516e-05f) / _S24) - 0.5f) - 0.25f) * _S24);
                    }
                    else
                    {
                        d_0 = d_3;
                    }
                    shadow_0 = 0.0f;
                }
                else
                {
                    if(kind_0 == i32(5))
                    {
                        var _S25 : f32 = (1.0f - smoothstep(_S8.shape_0.y, 1.0f, length(p_2) / halfSize_1.x)) * _S8.color_0.w;
                        d_0 = 1000.0f;
                        shadow_0 = _S25;
                    }
                    else
                    {
                        var cell_0 : vec2<f32> = _S12 / vec2<f32>(5.0f, 7.0f);
                        var local_0 : vec2<f32> = _S8.design_0 - _S9;
                        var _S26 : vec2<f32> = vec2<f32>(0.0f);
                        const _S27 : vec2<f32> = vec2<f32>(4.0f, 6.0f);
                        var index_0 : vec2<f32> = clamp(floor(local_0 / cell_0), _S26, _S27);
                        var low_1 : u32 = u32(_S8.shape_0.y + 0.5f);
                        var high_1 : u32 = u32(_S8.shape_0.z + 0.5f);
                        if(glyphDot_0(low_1, high_1, u32(index_0.x), u32(index_0.y)))
                        {
                            d_0 = roundedBox_0(local_0 - (index_0 + _S11) * cell_0, cell_0 * vec2<f32>(0.41999998688697815f), min(cell_0.x, cell_0.y) * 0.18000000715255737f);
                        }
                        else
                        {
                            d_0 = 1000.0f;
                        }
                        var cast_0 : vec2<f32> = local_0 - cell_0 * vec2<f32>(0.2199999988079071f);
                        var under_0 : vec2<f32> = clamp(floor(cast_0 / cell_0), _S26, _S27);
                        var _S28 : f32 = cell_0.x;
                        if(((_S28 * 0.2199999988079071f) > (_S13 * 0.5f)))
                        {
                            _S14 = (all(((cast_0 >= vec2<f32>(0.0f)))));
                        }
                        else
                        {
                            _S14 = false;
                        }
                        if(_S14)
                        {
                            _S14 = glyphDot_0(low_1, high_1, u32(under_0.x), u32(under_0.y));
                        }
                        else
                        {
                            _S14 = false;
                        }
                        if(_S14)
                        {
                            shadow_0 = clamp(0.5f - roundedBox_0(cast_0 - (under_0 + _S11) * cell_0, cell_0 * vec2<f32>(0.41999998688697815f), min(_S28, cell_0.y) * 0.18000000715255737f) / _S13, 0.0f, 1.0f) * 0.5f * _S8.color_0.w;
                        }
                        else
                        {
                            shadow_0 = 0.0f;
                        }
                    }
                }
            }
            normal_3 = normal_2;
        }
    }
    var coverage_0 : f32 = clamp(0.5f - d_0 / _S13, 0.0f, 1.0f);
    var _S29 : bool = (coverage_0 <= 0.0f);
    if(_S29)
    {
        _S14 = (shadow_0 <= 0.0f);
    }
    else
    {
        _S14 = false;
    }
    if(_S14)
    {
        discard;
    }
    var o_0 : FragmentOut_0;
    var _S30 : vec4<f32>;
    if(_S29)
    {
        if(isGlowPass_0())
        {
            _S30 = vec4<f32>(0.0f);
        }
        else
        {
            _S30 = vec4<f32>(0.0f, 0.0f, 0.0f, shadow_0);
        }
        o_0.fragColor_0 = _S30;
        return o_0;
    }
    var _S31 : vec3<f32> = _S8.color_0.xyz;
    var albedo_1 : vec3<f32> = toLinear_0(_S31);
    var _S32 : bool = ((_S8.extra_0.z) > 0.0f);
    if(_S32)
    {
        _S14 = kind_0 == i32(1);
    }
    else
    {
        _S14 = false;
    }
    var shine_1 : f32;
    if(_S14)
    {
        shine_1 = 0.85000002384185791f;
    }
    else
    {
        shine_1 = 0.15000000596046448f;
    }
    var _S33 : vec3<f32> = vec3<f32>(0.0f);
    var _S34 : vec3<f32> = vec3<f32>(_S8.glow_0);
    var colour_0 : vec3<f32> = mix(shade_0(_S8.worldPosition_0, normal_3, albedo_1, shine_1, _S33), albedo_1 * vec3<f32>(1.6000000238418579f), _S34);
    if(kind_0 == i32(1))
    {
        _S14 = _S32;
    }
    else
    {
        _S14 = false;
    }
    var colour_1 : vec3<f32>;
    if(_S14)
    {
        colour_1 = colour_0 + shade_0(_S8.worldPosition_0, normal_3, _S33, shine_1, _S33) * _S34;
    }
    else
    {
        colour_1 = colour_0;
    }
    if(isGlowPass_0())
    {
        _S30 = vec4<f32>(_S31 * _S34, _S8.color_0.w * coverage_0);
    }
    else
    {
        _S30 = vec4<f32>(toSrgb_0(rollOff_0(colour_1)), max(_S8.color_0.w * coverage_0, shadow_0));
    }
    o_0.fragColor_0 = _S30;
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
