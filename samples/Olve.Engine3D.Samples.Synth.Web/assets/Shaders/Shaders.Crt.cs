// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
global using CrtShaderId = Olve.Engine3D.Rendering.Shaders.ShaderId<Olve.Generated.Shaders.Shaders.Shared.LitVertex, Olve.Generated.Shaders.Shaders.Crt.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Generated.Shaders.Shaders.Shared.SpacePass, Olve.Generated.Shaders.Shaders.Crt.Inputs>;
global using CrtGroupId = Olve.Engine3D.Rendering.Instancing.GroupId<Olve.Generated.Shaders.Shaders.Shared.LitVertex, Olve.Generated.Shaders.Shaders.Crt.Instance>;
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

    public partial class Crt : IShaderDefinition<Shared.LitVertex, Crt.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Generated.Shaders.Shaders.Shared.SpacePass, Olve.Generated.Shaders.Shaders.Crt.Inputs>
    {
        /// <summary>
        /// This shader's definition, registered with <c>ShaderManager.Register</c>. Definitions are immutable, so each shader has exactly one.
        /// </summary>
        public static Crt Definition { get; } = new();

        private Crt()
        {
        }

        /// <summary>
        /// The values and textures of one rendering group drawn with this shader.
        /// </summary>
        public readonly record struct Inputs : IGroupInputs
        {
            public required TextureSampler<ITextureFormat.Rgba8Unorm, ISamplerKind.Filtering> UTrace { get; init; }

            public static int FloatCount => 0;

            public void WriteTo(Span<float> buffer)
            {
            }

            public static int TextureCount => 1;

            public void WriteTextures(Span<TextureBinding> textures)
            {
                textures[0] = new TextureBinding(UTrace.Texture, UTrace.Sampler);
            }
        }


        public readonly record struct Instance(
            Matrix4x4 iWorld,
            Vector4 iScreen,
            Vector4 iPhosphor
        ) : IInstanceData
        {
            public static int FloatCount => 24;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = iWorld.M11; buffer[i++] = iWorld.M12; buffer[i++] = iWorld.M13; buffer[i++] = iWorld.M14; buffer[i++] = iWorld.M21; buffer[i++] = iWorld.M22; buffer[i++] = iWorld.M23; buffer[i++] = iWorld.M24; buffer[i++] = iWorld.M31; buffer[i++] = iWorld.M32; buffer[i++] = iWorld.M33; buffer[i++] = iWorld.M34; buffer[i++] = iWorld.M41; buffer[i++] = iWorld.M42; buffer[i++] = iWorld.M43; buffer[i++] = iWorld.M44;
                buffer[i++] = iScreen.X; buffer[i++] = iScreen.Y; buffer[i++] = iScreen.Z; buffer[i++] = iScreen.W;
                buffer[i++] = iPhosphor.X; buffer[i++] = iPhosphor.Y; buffer[i++] = iPhosphor.Z; buffer[i++] = iPhosphor.W;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 96,
                Instanced: true,
                Attributes:
                [
                    new(Location: 2, ComponentCount: 4, ByteOffset: 0), // iWorld[0]
                    new(Location: 3, ComponentCount: 4, ByteOffset: 16), // iWorld[1]
                    new(Location: 4, ComponentCount: 4, ByteOffset: 32), // iWorld[2]
                    new(Location: 5, ComponentCount: 4, ByteOffset: 48), // iWorld[3]
                    new(Location: 6, ComponentCount: 4, ByteOffset: 64), // iScreen
                    new(Location: 7, ComponentCount: 4, ByteOffset: 80), // iPhosphor
                ]);
        }

        #region ShaderSource

        public ShaderData ShaderData { get; } = new()
        {
            Name = "Crt",
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
    @location(3) screen_0 : vec4<f32>,
    @location(4) phosphor_0 : vec3<f32>,
};

struct vertexInput_0
{
    @location(0) position_1 : vec3<f32>,
    @location(1) normal_1 : vec3<f32>,
    @location(2) row0_0 : vec4<f32>,
    @location(3) row1_0 : vec4<f32>,
    @location(4) row2_0 : vec4<f32>,
    @location(5) row3_0 : vec4<f32>,
    @location(6) iScreen_0 : vec4<f32>,
    @location(7) iPhosphor_0 : vec4<f32>,
};

struct vertexInput_1
{
     _S5 : vec3<f32>,
     _S6 : vec3<f32>,
     iWorld_0 : Matrix4Rows_0,
     _S7 : vec4<f32>,
     _S8 : vec4<f32>,
};

@vertex
fn vertexMain( _S9 : vertexInput_0) -> VertexOut_0
{
    var _S10 : vertexInput_1;
    _S10._S5 = _S9.position_1;
    _S10._S6 = _S9.normal_1;
    _S10.iWorld_0._S1 = _S9.row0_0;
    _S10.iWorld_0._S2 = _S9.row1_0;
    _S10.iWorld_0._S3 = _S9.row2_0;
    _S10.iWorld_0._S4 = _S9.row3_0;
    _S10._S7 = _S9.iScreen_0;
    _S10._S8 = _S9.iPhosphor_0;
    var world_0 : mat4x4<f32> = Matrix4Rows_toMatrix_0(_S10.iWorld_0);
    var worldPosition_1 : vec4<f32> = (((world_0) * (vec4<f32>(_S10._S5, 1.0f))));
    var o_0 : VertexOut_0;
    o_0.worldPosition_0 = worldPosition_1.xyz;
    o_0.localPosition_0 = _S10._S5;
    o_0.normal_0 = (((_S10._S6) * (inverse3x3_0(mat3x3<f32>(world_0[i32(0)].xyz, world_0[i32(1)].xyz, world_0[i32(2)].xyz)))));
    o_0.screen_0 = _S10._S7;
    o_0.phosphor_0 = _S10._S8.xyz;
    o_0.position_0 = (((mat4x4<f32>(spacePass_0.projection_0.data_0[i32(0)][i32(0)], spacePass_0.projection_0.data_0[i32(0)][i32(1)], spacePass_0.projection_0.data_0[i32(0)][i32(2)], spacePass_0.projection_0.data_0[i32(0)][i32(3)], spacePass_0.projection_0.data_0[i32(1)][i32(0)], spacePass_0.projection_0.data_0[i32(1)][i32(1)], spacePass_0.projection_0.data_0[i32(1)][i32(2)], spacePass_0.projection_0.data_0[i32(1)][i32(3)], spacePass_0.projection_0.data_0[i32(2)][i32(0)], spacePass_0.projection_0.data_0[i32(2)][i32(1)], spacePass_0.projection_0.data_0[i32(2)][i32(2)], spacePass_0.projection_0.data_0[i32(2)][i32(3)], spacePass_0.projection_0.data_0[i32(3)][i32(0)], spacePass_0.projection_0.data_0[i32(3)][i32(1)], spacePass_0.projection_0.data_0[i32(3)][i32(2)], spacePass_0.projection_0.data_0[i32(3)][i32(3)])) * ((((mat4x4<f32>(spacePass_0.view_0.data_0[i32(0)][i32(0)], spacePass_0.view_0.data_0[i32(0)][i32(1)], spacePass_0.view_0.data_0[i32(0)][i32(2)], spacePass_0.view_0.data_0[i32(0)][i32(3)], spacePass_0.view_0.data_0[i32(1)][i32(0)], spacePass_0.view_0.data_0[i32(1)][i32(1)], spacePass_0.view_0.data_0[i32(1)][i32(2)], spacePass_0.view_0.data_0[i32(1)][i32(3)], spacePass_0.view_0.data_0[i32(2)][i32(0)], spacePass_0.view_0.data_0[i32(2)][i32(1)], spacePass_0.view_0.data_0[i32(2)][i32(2)], spacePass_0.view_0.data_0[i32(2)][i32(3)], spacePass_0.view_0.data_0[i32(3)][i32(0)], spacePass_0.view_0.data_0[i32(3)][i32(1)], spacePass_0.view_0.data_0[i32(3)][i32(2)], spacePass_0.view_0.data_0[i32(3)][i32(3)])) * (worldPosition_1))))));
    return o_0;
}

",
                FragmentSource = @"@binding(1) @group(0) var uTrace_texture_0 : texture_2d<f32>;

@binding(2) @group(0) var uTrace_sampler_0 : sampler;

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

fn outside_0( p_0 : vec2<f32>,  halfSize_0 : vec2<f32>) -> f32
{
    return length(max(abs(p_0) - halfSize_0, vec2<f32>(0.0f)));
}

fn toSrgb_0( c_1 : vec3<f32>) -> vec3<f32>
{
    return pow(max(c_1, vec3<f32>(0.0f)), vec3<f32>(0.45454543828964233f));
}

fn rollOff_0( c_2 : vec3<f32>) -> vec3<f32>
{
    var knee_0 : vec3<f32> = vec3<f32>(0.75f);
    var _S2 : vec3<f32> = max(c_2 - knee_0, vec3<f32>(0.0f));
    return min(c_2, knee_0) + _S2 / (vec3<f32>(1.0f) + _S2 * vec3<f32>(2.0f));
}

struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) worldPosition_0 : vec3<f32>,
    @location(1) localPosition_0 : vec3<f32>,
    @location(2) normal_1 : vec3<f32>,
    @location(3) screen_0 : vec4<f32>,
    @location(4) phosphor_0 : vec3<f32>,
};

fn shade_0( _S3 : vec3<f32>,  _S4 : vec3<f32>,  _S5 : vec3<f32>,  _S6 : f32,  _S7 : vec3<f32>) -> vec3<f32>
{
    var toEye_1 : vec3<f32> = normalize(spacePass_0.cameraPosition_0 - _S3);
    return _S5 * mix(spacePass_0.groundAmbient_0, spacePass_0.skyAmbient_0, vec3<f32>((_S4.y * 0.5f + 0.5f))) + whiteLight_0(spacePass_0.keyPosition_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.keyColor_0 + whiteLight_0(spacePass_0.light0Position_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.light0Color_0 + whiteLight_0(spacePass_0.light1Position_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.light1Color_0 + _S7;
}

fn isGlowPass_0() -> bool
{
    return ((spacePass_0.glowOnly_0.x) > 0.5f);
}

@fragment
fn fragmentMain( _S8 : pixelInput_0, @builtin(position) position_1 : vec4<f32>) -> FragmentOut_0
{
    var size_0 : vec2<f32> = _S8.screen_0.xy;
    var local_0 : vec2<f32> = _S8.localPosition_0.xy;
    var phosphor_1 : vec3<f32> = toLinear_0(_S8.phosphor_0);
    var centred_0 : vec2<f32> = local_0 / size_0;
    var bent_0 : vec2<f32> = centred_0 * vec2<f32>((1.0f + 0.090000003576278687f * dot(centred_0, centred_0)));
    var _S9 : f32 = bent_0.x;
    var _S10 : f32 = bent_0.y;
    var _S11 : f32 = 0.5f - _S10;
    var uv_0 : vec2<f32> = vec2<f32>(_S9 + 0.5f, _S11);
    var inside_0 : f32 = 1.0f - smoothstep(0.4699999988079071f, 0.5f, max(abs(_S9), abs(_S10)));
    ;
    var cells_0 : vec2<f32> = uv_0 * vec2<f32>(10.0f, 8.0f);
    var _S12 : vec2<f32> = vec2<f32>(0.5f);
    var toLine_0 : vec2<f32> = abs(fract(cells_0 + _S12) - _S12) / (fwidth((cells_0)));
    var toMiddle_0 : vec2<f32> = abs(uv_0 - _S12) / (fwidth((uv_0)));
    var _S13 : vec3<f32> = toLinear_0(max((textureSample((uTrace_texture_0), (uTrace_sampler_0), (uv_0))).xyz - vec3<f32>(0.011764706112444401f), vec3<f32>(0.0f)));
    var _S14 : vec3<f32> = vec3<f32>((1.0f - smoothstep(0.25f, 0.75f, length(bent_0 * vec2<f32>(1.0f, 1.25f)))));
    var _S15 : vec3<f32> = vec3<f32>(inside_0);
    var _S16 : vec3<f32> = shade_0(_S8.worldPosition_0, normalize(_S8.normal_1), vec3<f32>(0.0040000001899898052f, 0.0060000000521540642f, 0.004999999888241291f), 0.94999998807907104f, (_S13 * vec3<f32>(1.7999999523162842f) * vec3<f32>((0.86000001430511475f + 0.14000000059604645f * sin(_S11 * 3.1415927410125732f * 2.0f * 110.0f))) + phosphor_1 * vec3<f32>((max((1.0f - saturate(min(toLine_0.x, toLine_0.y) - 0.5f)) * 0.035000000149011612f, (1.0f - saturate(min(toMiddle_0.x, toMiddle_0.y) - 0.5f)) * 0.059999998658895493f) + 0.0060000000521540642f))) * _S14 * _S15);
    var o_0 : FragmentOut_0;
    if(((_S8.screen_0.z) > 0.5f))
    {
        var halo_0 : f32 = exp(- outside_0(local_0, size_0 * vec2<f32>(0.41999998688697815f)) * 90.0f) * _S8.screen_0.w;
        var _S17 : vec3<f32> = toSrgb_0(phosphor_1);
        var _S18 : f32;
        if(isGlowPass_0())
        {
            _S18 = 0.11999999731779099f;
        }
        else
        {
            _S18 = 0.34999999403953552f;
        }
        o_0.fragColor_0 = vec4<f32>(_S17, halo_0 * _S18);
        return o_0;
    }
    var traceGlow_0 : vec3<f32> = _S13 * _S14 * _S15;
    var _S19 : vec4<f32>;
    if(isGlowPass_0())
    {
        _S19 = vec4<f32>(toSrgb_0(traceGlow_0), 1.0f);
    }
    else
    {
        _S19 = vec4<f32>(toSrgb_0(rollOff_0(_S16)), 1.0f);
    }
    o_0.fragColor_0 = _S19;
    return o_0;
}

",
                Pass = new WgslInputsBindings(0, []),
                Group = new WgslInputsBindings(null, [new WgslTextureBinding(1, 2)]),
            },
        };

        #endregion
    }
}
